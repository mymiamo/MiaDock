using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MiaDock.Core.Overlay;
using MiaDock.Core.Presentation;
using MiaDock.Platform.Windows.Interop;
using MiaDock.Platform.Windows.Display;

namespace MiaDock.Platform.Windows.Overlay;

internal sealed class OverlayWindowController : IOverlayWindowController
{
    private const nuint SubclassId = 0x4D494144;
    private static readonly TimeSpan EdgeRevealAnimationDuration = TimeSpan.FromMilliseconds(170);
    private readonly Window _window;
    private readonly AppWindow _appWindow;
    private readonly OverlayWindowOptions _options;
    private readonly IOverlayPlacementCalculator _placementCalculator;
    private readonly IDisplayTopologyService _displayTopology;
    private readonly NativeMethods.SubclassProcedure _subclassProcedure;
    private readonly NativeMethods.LowLevelMouseProcedure _mouseProcedure;
    private OverlaySize _sizeInDips;
    private DockCornerRadii _cornerRadiiInDips;
    private double _marginInDips;
    private OverlayPlacement? _lastPlacement;
    private OverlayPlacement? _lastVisiblePlacement;
    private OverlayWorkArea? _lastDisplayBounds;
    private uint _lastDpi;
    private bool _hasClearedRegion;
    private bool _disposed;
    private bool _subclassInstalled;
    private bool _inputActivationEnabled;
    private nint _mouseHook;
    private int _outsideClickPending;
    private OverlayPosition _position;
    private string? _displayId;
    private bool _edgeRevealHidden;
    private double _edgeRevealStripInDips = 2;
    private DispatcherQueueTimer? _edgeRevealAnimationTimer;
    private long _edgeRevealAnimationStartTimestamp;
    private OverlayPlacement? _edgeRevealAnimationStartPlacement;
    private OverlayPlacement? _edgeRevealAnimatedPlacement;
    private OverlayPlacement? _edgeRevealAnimationTargetPlacement;
    private Action? _edgeRevealAnimationCompleted;

    public OverlayWindowController(
        Window window,
        OverlayWindowOptions options,
        IOverlayPlacementCalculator placementCalculator,
        IDisplayTopologyService displayTopology)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(placementCalculator);
        ArgumentNullException.ThrowIfNull(displayTopology);

        if (!options.InitialSize.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The initial overlay size is invalid.");
        }

        _window = window;
        _appWindow = window.AppWindow;
        _options = options;
        _placementCalculator = placementCalculator;
        _displayTopology = displayTopology;
        _sizeInDips = options.InitialSize;
        _cornerRadiiInDips = options.CornerRadiiInDips;
        _marginInDips = options.MarginInDips;
        _position = options.Position;
        _subclassProcedure = WindowMessageHandler;
        _mouseProcedure = LowLevelMouseHandler;

        WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (WindowHandle == 0)
        {
            throw new InvalidOperationException("Unable to retrieve the overlay window handle.");
        }

        ConfigurePresenter();
        SuppressWindowChrome();
        EnableTransparentCompositionSurface();
        InstallSubclass();
        _window.Closed += OnWindowClosed;
        _displayTopology.DisplaysChanged += OnDisplaysChanged;
    }

    public nint WindowHandle { get; }

    public Exception? LastFailure { get; private set; }

    public bool IsVisible { get; private set; }

    public event EventHandler? OutsidePointerPressed;

    public void ShowNoActivate()
    {
        ThrowIfDisposed();
        Reposition();
        _ = NativeMethods.ShowWindow(WindowHandle, NativeConstants.SwShowNoActivate);
        IsVisible = true;
        // WinUI can restore frame styles during the first HWND presentation.
        SuppressWindowChrome();
    }

    public void Hide()
    {
        ThrowIfDisposed();
        StopEdgeRevealAnimation();
        _ = NativeMethods.ShowWindow(WindowHandle, NativeConstants.SwHide);
        IsVisible = false;
    }

    public void UpdatePlacement(OverlayPosition position, string? displayId, double marginInDips)
    {
        ThrowIfDisposed();
        if (!double.IsFinite(marginInDips) || marginInDips < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(marginInDips));
        }

        _position = position;
        _displayId = displayId;
        _marginInDips = marginInDips;
        StopEdgeRevealAnimation();
        Reposition();
    }

    public void UpdateLayout(OverlaySize sizeInDips, DockCornerRadii cornerRadiiInDips)
    {
        ThrowIfDisposed();

        if (!sizeInDips.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInDips));
        }

        if (!AreValid(cornerRadiiInDips))
        {
            throw new ArgumentOutOfRangeException(nameof(cornerRadiiInDips));
        }

        _sizeInDips = sizeInDips;
        _cornerRadiiInDips = cornerRadiiInDips;
        StopEdgeRevealAnimation();
        Reposition();
    }

    public void UpdateOpacity(double opacity)
    {
        ThrowIfDisposed();
        if (!double.IsFinite(opacity) || opacity is < 0.1 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(opacity));
        }

        // Surface transparency belongs to the XAML island background. The HWND
        // must remain non-layered so Desktop Acrylic can sample what is behind it.
    }

    public void SetOutsideClickMonitoring(bool enabled)
    {
        ThrowIfDisposed();
        if (enabled == (_mouseHook != 0))
        {
            return;
        }

        if (!enabled)
        {
            StopOutsideClickMonitoring();
            return;
        }

        var moduleHandle = NativeMethods.GetModuleHandle(null);
        _mouseHook = NativeMethods.SetWindowsHookEx(
            NativeConstants.WhMouseLowLevel,
            _mouseProcedure,
            moduleHandle,
            0);
        if (_mouseHook == 0)
        {
            LastFailure = new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Unable to monitor pointer presses outside the expanded overlay.");
        }
    }

    public void SetInputActivationEnabled(bool enabled)
    {
        ThrowIfDisposed();
        if (_inputActivationEnabled == enabled)
        {
            return;
        }

        _inputActivationEnabled = enabled;
        ApplyExtendedStyles();
    }

    public void SetEdgeRevealHidden(
        bool hidden,
        double visibleStripInDips = 2,
        bool animate = false,
        Action? transitionCompleted = null)
    {
        ThrowIfDisposed();
        if (!double.IsFinite(visibleStripInDips) || visibleStripInDips is < 1 or > 160)
        {
            throw new ArgumentOutOfRangeException(nameof(visibleStripInDips));
        }

        var isUnchanged = _edgeRevealHidden == hidden &&
                          Math.Abs(_edgeRevealStripInDips - visibleStripInDips) < 0.01;
        if (isUnchanged && _edgeRevealAnimationTargetPlacement is null)
        {
            transitionCompleted?.Invoke();
            return;
        }

        if (isUnchanged && animate)
        {
            return;
        }

        var currentPlacement = _edgeRevealAnimatedPlacement ?? _lastPlacement;
        StopEdgeRevealAnimation();
        _edgeRevealHidden = hidden;
        _edgeRevealStripInDips = visibleStripInDips;
        var layout = CalculatePlacement();
        if (!animate || !IsVisible || currentPlacement is null || currentPlacement == layout.Placement)
        {
            ApplyPlacement(layout);
            transitionCompleted?.Invoke();
            return;
        }

        StartEdgeRevealAnimation(currentPlacement.Value, layout, transitionCompleted);
    }

    public bool IsPointerAtAttachedEdge(
        int activationThicknessInPixels = 3,
        int spanPaddingInPixels = 24)
    {
        ThrowIfDisposed();
        if (_lastVisiblePlacement is not { } placement ||
            _lastDisplayBounds is not { } displayBounds ||
            !NativeMethods.GetCursorPos(out var point))
        {
            return false;
        }

        return DockEdgeRevealGeometry.IsPointerAtActivationEdge(
            point.X,
            point.Y,
            displayBounds,
            placement,
            _position,
            activationThicknessInPixels,
            spanPaddingInPixels);
    }

    public bool IsPointerOverWindow()
    {
        ThrowIfDisposed();
        if (!NativeMethods.GetCursorPos(out var point) ||
            !NativeMethods.GetWindowRect(WindowHandle, out var bounds))
        {
            return false;
        }

        return point.X >= bounds.Left &&
               point.X < bounds.Right &&
               point.Y >= bounds.Top &&
               point.Y < bounds.Bottom &&
               RoundedRectangleHitTest.Contains(
                   point.X - bounds.Left,
                   point.Y - bounds.Top,
                   bounds.Width,
                   bounds.Height,
                   GetCornerRadiiInPixels());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Native window destruction can synchronously re-enter WM_NCDESTROY. Set the
        // guard first so cleanup cannot recursively execute on the same native stack.
        _disposed = true;
        StopEdgeRevealAnimation();

        _window.Closed -= OnWindowClosed;
        _displayTopology.DisplaysChanged -= OnDisplaysChanged;
        if (_subclassInstalled)
        {
            _ = NativeMethods.RemoveWindowSubclass(WindowHandle, _subclassProcedure, SubclassId);
            _subclassInstalled = false;
        }

        StopOutsideClickMonitoring();
    }

    private void ConfigurePresenter()
    {
        if (_appWindow.Presenter is not OverlappedPresenter presenter)
        {
            throw new NotSupportedException("The overlay requires an OverlappedPresenter.");
        }

        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsAlwaysOnTop = true;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        presenter.IsResizable = false;
    }

    private void ApplyExtendedStyles()
    {
        Marshal.SetLastPInvokeError(0);
        var currentStyles = NativeMethods.GetWindowLongPtr(WindowHandle, NativeConstants.GwlExStyle);
        var error = Marshal.GetLastPInvokeError();
        if (currentStyles == 0 && error != 0)
        {
            throw new Win32Exception(error, "Unable to read overlay window styles.");
        }

        var desiredStyles = WindowStylePolicy.ApplyOverlayStyles(
            currentStyles.ToInt64(),
            _inputActivationEnabled);
        Marshal.SetLastPInvokeError(0);
        var previousStyles = NativeMethods.SetWindowLongPtr(
            WindowHandle,
            NativeConstants.GwlExStyle,
            new nint(desiredStyles));
        error = Marshal.GetLastPInvokeError();
        if (previousStyles == 0 && error != 0)
        {
            throw new Win32Exception(error, "Unable to apply overlay window styles.");
        }

        EnsureSetWindowPos(
            0,
            0,
            0,
            0,
            nint.Zero,
            NativeConstants.SwpNoMove
            | NativeConstants.SwpNoSize
            | NativeConstants.SwpNoZOrder
            | NativeConstants.SwpNoActivate
            | NativeConstants.SwpFrameChanged);
    }

    private void InstallSubclass()
    {
        if (!NativeMethods.SetWindowSubclass(WindowHandle, _subclassProcedure, SubclassId, 0))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Unable to monitor overlay window messages.");
        }

        _subclassInstalled = true;
    }

    private void ApplyStandardStyles()
    {
        Marshal.SetLastPInvokeError(0);
        var currentStyles = NativeMethods.GetWindowLongPtr(WindowHandle, NativeConstants.GwlStyle);
        var error = Marshal.GetLastPInvokeError();
        if (currentStyles == 0 && error != 0)
        {
            throw new Win32Exception(error, "Unable to read overlay window styles.");
        }

        var desiredStyles = WindowStylePolicy.ApplyOverlayWindowStyles(currentStyles.ToInt64());
        Marshal.SetLastPInvokeError(0);
        var previousStyles = NativeMethods.SetWindowLongPtr(
            WindowHandle,
            NativeConstants.GwlStyle,
            new nint(desiredStyles));
        error = Marshal.GetLastPInvokeError();
        if (previousStyles == 0 && error != 0)
        {
            throw new Win32Exception(error, "Unable to remove overlay window frame styles.");
        }
    }

    private void ConfigureDwmAppearance()
    {
        SetDwmAttribute(
            NativeConstants.DwmwaNcRenderingPolicy,
            NativeConstants.DwmNcRenderingDisabled,
            "Unable to disable DWM non-client rendering for the overlay.");
        SetDwmAttribute(
            NativeConstants.DwmwaWindowCornerPreference,
            NativeConstants.DwmWindowCornerDoNotRound,
            "Unable to disable the DWM window corner for the overlay.");
        SetDwmAttribute(
            NativeConstants.DwmwaBorderColor,
            NativeConstants.DwmColorNone,
            "Unable to suppress the DWM overlay border.");
    }

    private void SuppressWindowChrome()
    {
        ApplyStandardStyles();
        ApplyExtendedStyles();
        ConfigureDwmAppearance();
    }

    private void EnableTransparentCompositionSurface()
    {
        // An empty blur region tells DWM to preserve the alpha channel supplied
        // by the window's composition backdrop without applying a rectangular
        // blur across the HWND.
        var emptyRegion = NativeMethods.CreateRectRgn(-2, -2, -1, -1);
        if (emptyRegion == 0)
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Unable to create the transparent composition region.");
        }

        try
        {
            var blurBehind = new NativeDwmBlurBehind
            {
                Flags = NativeConstants.DwmBlurBehindEnable | NativeConstants.DwmBlurBehindRegion,
                Enable = true,
                BlurRegion = emptyRegion
            };
            var result = NativeMethods.DwmEnableBlurBehindWindow(WindowHandle, ref blurBehind);
            if (result < 0)
            {
                throw new COMException(
                    "Unable to enable the transparent DWM composition surface.",
                    result);
            }
        }
        finally
        {
            _ = NativeMethods.DeleteObject(emptyRegion);
        }
    }

    private void SetDwmAttribute(int attribute, uint value, string failureMessage)
    {
        var result = NativeMethods.DwmSetWindowAttribute(
            WindowHandle,
            attribute,
            ref value,
            sizeof(uint));
        if (result < 0)
        {
            throw new COMException(failureMessage, result);
        }
    }

    private void Reposition()
    {
        ApplyPlacement(CalculatePlacement());
    }

    private EdgeRevealLayout CalculatePlacement()
    {
        var dpi = NativeMethods.GetDpiForWindow(WindowHandle);
        if (dpi == 0)
        {
            throw new Win32Exception("Unable to determine overlay DPI.");
        }

        var display = _displayTopology.Find(_displayId) ?? _displayTopology.Primary;
        var workArea = display.WorkArea;
        var visiblePlacement = _placementCalculator.Calculate(new OverlayLayoutRequest(
            new OverlayWorkArea(workArea.X, workArea.Y, workArea.Width, workArea.Height),
            _sizeInDips,
            dpi,
            _position,
            _marginInDips));
        var displayBounds = new OverlayWorkArea(
            display.Bounds.X,
            display.Bounds.Y,
            display.Bounds.Width,
            display.Bounds.Height);
        var visibleStripPixels = Math.Max(
            1,
            checked((int)Math.Round(_edgeRevealStripInDips * dpi / 96d)));
        var placement = _edgeRevealHidden
            ? DockEdgeRevealGeometry.HideTowardAttachedEdge(
                visiblePlacement,
                displayBounds,
                _position,
                visibleStripPixels)
            : visiblePlacement;

        return new EdgeRevealLayout(placement, visiblePlacement, displayBounds, dpi);
    }

    private void ApplyPlacement(EdgeRevealLayout layout)
    {
        ApplyPlacement(layout.Placement);

        // SetWindowRgn masks are permanently 1-bit and turn the rounded corners
        // into a staircase. The backdrop element carries the silhouette instead,
        // so the HWND stays rectangular and DWM keeps the anti-aliased alpha.
        ClearWindowRegionIfNeeded();
        _lastVisiblePlacement = layout.VisiblePlacement;
        _lastDisplayBounds = layout.DisplayBounds;
        _lastDpi = layout.Dpi;
        LastFailure = null;
    }

    private void ApplyPlacement(OverlayPlacement placement)
    {
        EnsureSetWindowPos(
            placement.X,
            placement.Y,
            placement.Width,
            placement.Height,
            NativeConstants.HwndTopmost,
            NativeConstants.SwpNoActivate
            | NativeConstants.SwpNoOwnerZOrder);
        _lastPlacement = placement;
    }

    private void StartEdgeRevealAnimation(
        OverlayPlacement from,
        EdgeRevealLayout target,
        Action? transitionCompleted)
    {
        _edgeRevealAnimationStartPlacement = from;
        _edgeRevealAnimatedPlacement = from;
        _edgeRevealAnimationTargetPlacement = target.Placement;
        _edgeRevealAnimationCompleted = transitionCompleted;
        _edgeRevealAnimationStartTimestamp = Stopwatch.GetTimestamp();
        _lastVisiblePlacement = target.VisiblePlacement;
        _lastDisplayBounds = target.DisplayBounds;
        _lastDpi = target.Dpi;
        _edgeRevealAnimationTimer ??= _window.DispatcherQueue.CreateTimer();
        _edgeRevealAnimationTimer.Interval = TimeSpan.FromMilliseconds(16);
        _edgeRevealAnimationTimer.IsRepeating = true;
        _edgeRevealAnimationTimer.Tick -= OnEdgeRevealAnimationTick;
        _edgeRevealAnimationTimer.Tick += OnEdgeRevealAnimationTick;
        _edgeRevealAnimationTimer.Start();
    }

    private void OnEdgeRevealAnimationTick(DispatcherQueueTimer sender, object args)
    {
        try
        {
            if (_edgeRevealAnimationStartPlacement is not { } from ||
                _edgeRevealAnimationTargetPlacement is not { } to)
            {
                StopEdgeRevealAnimation();
                return;
            }

            var elapsed = Stopwatch.GetElapsedTime(_edgeRevealAnimationStartTimestamp);
            var progress = Math.Clamp(elapsed.TotalMilliseconds / EdgeRevealAnimationDuration.TotalMilliseconds, 0d, 1d);
            var easedProgress = 1d - Math.Pow(1d - progress, 3d);
            var placement = InterpolatePlacement(from, to, easedProgress);
            ApplyPlacement(placement);
            _edgeRevealAnimatedPlacement = placement;

            if (progress < 1d)
            {
                return;
            }

            var completed = _edgeRevealAnimationCompleted;
            StopEdgeRevealAnimation();
            completed?.Invoke();
        }
        catch (Exception exception)
        {
            LastFailure = exception;
            StopEdgeRevealAnimation();
        }
    }

    private void StopEdgeRevealAnimation()
    {
        if (_edgeRevealAnimationTimer is not null)
        {
            _edgeRevealAnimationTimer.Stop();
            _edgeRevealAnimationTimer.Tick -= OnEdgeRevealAnimationTick;
        }

        _edgeRevealAnimatedPlacement = null;
        _edgeRevealAnimationStartPlacement = null;
        _edgeRevealAnimationTargetPlacement = null;
        _edgeRevealAnimationCompleted = null;
    }

    private static OverlayPlacement InterpolatePlacement(
        OverlayPlacement from,
        OverlayPlacement to,
        double progress) => new(
        (int)Math.Round(from.X + ((to.X - from.X) * progress)),
        (int)Math.Round(from.Y + ((to.Y - from.Y) * progress)),
        (int)Math.Round(from.Width + ((to.Width - from.Width) * progress)),
        (int)Math.Round(from.Height + ((to.Height - from.Height) * progress)));

    private void ClearWindowRegionIfNeeded()
    {
        if (_hasClearedRegion)
        {
            return;
        }

        if (NativeMethods.SetWindowRgn(WindowHandle, 0, true) == 0)
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Unable to clear the overlay window region.");
        }

        _hasClearedRegion = true;
    }

    private void EnsureSetWindowPos(
        int x,
        int y,
        int width,
        int height,
        nint insertAfter,
        uint flags)
    {
        if (!NativeMethods.SetWindowPos(WindowHandle, insertAfter, x, y, width, height, flags))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Unable to position the overlay window.");
        }
    }

    private nint WindowMessageHandler(
        nint windowHandle,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData)
    {
        try
        {
            if (TryHandleWindowMessage(message, lParam) is { } handled)
            {
                return handled;
            }
        }
        catch (Exception exception)
        {
            // The runtime fails fast when a managed exception unwinds into the
            // native window procedure, so the process dies without a log entry.
            // Every failure is captured here and the message falls through to
            // the default handler instead.
            LastFailure = exception;
        }

        return NativeMethods.DefSubclassProc(windowHandle, message, wParam, lParam);
    }

    private nint? TryHandleWindowMessage(uint message, nint lParam)
    {
        if (message == NativeConstants.WmMouseActivate)
        {
            if (!_inputActivationEnabled)
            {
                return NativeConstants.MaNoActivate;
            }
        }

        if (message == NativeConstants.WmNcHitTest &&
            NativeMethods.GetWindowRect(WindowHandle, out var hitTestBounds))
        {
            var point = RoundedRectangleHitTest.PointFromMessage(lParam);
            if (!RoundedRectangleHitTest.Contains(
                    point.X - hitTestBounds.Left,
                    point.Y - hitTestBounds.Top,
                    hitTestBounds.Right - hitTestBounds.Left,
                    hitTestBounds.Bottom - hitTestBounds.Top,
                    GetCornerRadiiInPixels()))
            {
                return NativeConstants.HtTransparent;
            }
        }
        else if (message is NativeConstants.WmDpiChanged
            or NativeConstants.WmDisplayChange
            or NativeConstants.WmSettingChange
            or NativeConstants.WmThemeChanged
            or NativeConstants.WmDwmCompositionChanged)
        {
            _ = EnqueueOnWindowThread(TryRefreshAppearanceAndPosition);
        }
        else if (message == NativeConstants.WmNcDestroy)
        {
            Dispose();
        }

        return null;
    }

    private nint LowLevelMouseHandler(int code, nuint message, nint data)
    {
        try
        {
            ObserveGlobalPointerMessage(code, message, data);
        }
        catch (Exception exception)
        {
            // A hook callback runs on a native stack as well, so an escaping
            // exception would terminate the process instead of being reported.
            LastFailure = exception;
        }

        return NativeMethods.CallNextHookEx(_mouseHook, code, message, data);
    }

    private void ObserveGlobalPointerMessage(int code, nuint message, nint data)
    {
        if (code >= 0 &&
            IsPointerDownMessage(message) &&
            NativeMethods.GetWindowRect(WindowHandle, out var bounds))
        {
            var hookData = Marshal.PtrToStructure<LowLevelMouseHookData>(data);
            var point = hookData.Point;
            var outside = point.X < bounds.Left ||
                          point.X >= bounds.Right ||
                          point.Y < bounds.Top ||
                          point.Y >= bounds.Bottom ||
                          !RoundedRectangleHitTest.Contains(
                              point.X - bounds.Left,
                              point.Y - bounds.Top,
                              bounds.Right - bounds.Left,
                              bounds.Bottom - bounds.Top,
                              GetCornerRadiiInPixels());
            if (outside && Interlocked.Exchange(ref _outsideClickPending, 1) == 0)
            {
                if (!EnqueueOnWindowThread(() =>
                    {
                        Interlocked.Exchange(ref _outsideClickPending, 0);
                        OutsidePointerPressed?.Invoke(this, EventArgs.Empty);
                    }))
                {
                    Interlocked.Exchange(ref _outsideClickPending, 0);
                }
            }
        }
    }

    private bool EnqueueOnWindowThread(Microsoft.UI.Dispatching.DispatcherQueueHandler callback)
    {
        // Broadcast messages and the global hook keep arriving while the window
        // tears down, and reading DispatcherQueue on a closed window throws.
        if (_disposed)
        {
            return false;
        }

        return _window.DispatcherQueue?.TryEnqueue(callback) == true;
    }

    private DockCornerRadii GetCornerRadiiInPixels()
    {
        var dpi = NativeMethods.GetDpiForWindow(WindowHandle);
        return dpi == 0 ? _cornerRadiiInDips : GetCornerRadiiInPixels(dpi);
    }

    private DockCornerRadii GetCornerRadiiInPixels(uint dpi) =>
        _cornerRadiiInDips.Scale(dpi / 96d);

    private static bool AreValid(DockCornerRadii radii) =>
        double.IsFinite(radii.TopLeft) && radii.TopLeft >= 0 &&
        double.IsFinite(radii.TopRight) && radii.TopRight >= 0 &&
        double.IsFinite(radii.BottomRight) && radii.BottomRight >= 0 &&
        double.IsFinite(radii.BottomLeft) && radii.BottomLeft >= 0;

    private static bool IsPointerDownMessage(nuint message) =>
        message is NativeConstants.WmLeftButtonDown
            or NativeConstants.WmRightButtonDown
            or NativeConstants.WmMiddleButtonDown
            or NativeConstants.WmXButtonDown;

    private void StopOutsideClickMonitoring()
    {
        if (_mouseHook != 0)
        {
            _ = NativeMethods.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = 0;
        }

        Interlocked.Exchange(ref _outsideClickPending, 0);
    }

    private void TryRefreshAppearanceAndPosition()
    {
        try
        {
            StopEdgeRevealAnimation();
            SuppressWindowChrome();
            Reposition();
        }
        catch (Exception exception)
        {
            LastFailure = exception;
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs args) => Dispose();

    private void OnDisplaysChanged(object? sender, IReadOnlyList<DisplayDescriptor> displays) =>
        _ = EnqueueOnWindowThread(TryRefreshAppearanceAndPosition);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private readonly record struct EdgeRevealLayout(
        OverlayPlacement Placement,
        OverlayPlacement VisiblePlacement,
        OverlayWorkArea DisplayBounds,
        uint Dpi);
}
