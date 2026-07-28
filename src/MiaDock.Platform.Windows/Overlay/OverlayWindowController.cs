using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MiaDock.Core.Overlay;
using MiaDock.Platform.Windows.Interop;
using MiaDock.Platform.Windows.Display;

namespace MiaDock.Platform.Windows.Overlay;

internal sealed class OverlayWindowController : IOverlayWindowController
{
    private const nuint SubclassId = 0x4D494144;
    private readonly Window _window;
    private readonly AppWindow _appWindow;
    private readonly OverlayWindowOptions _options;
    private readonly IOverlayPlacementCalculator _placementCalculator;
    private readonly IDisplayTopologyService _displayTopology;
    private readonly NativeMethods.SubclassProcedure _subclassProcedure;
    private readonly NativeMethods.LowLevelMouseProcedure _mouseProcedure;
    private readonly LayeredRoundedBackdropWindow _backdropWindow;
    private OverlaySize _sizeInDips;
    private double _cornerRadiusInDips;
    private uint _surfaceArgb = 0xFF000000;
    private OverlayPlacement? _lastPlacement;
    private uint _lastDpi;
    private bool _disposed;
    private bool _subclassInstalled;
    private nint _mouseHook;
    private int _outsideClickPending;
    private OverlayPosition _position;
    private string? _displayId;

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
        _cornerRadiusInDips = options.CornerRadiusInDips;
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
        _backdropWindow = new LayeredRoundedBackdropWindow();
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
        UpdateBackdropFromLastPlacement();
        // WinUI can restore frame styles during the first HWND presentation.
        SuppressWindowChrome();
    }

    public void Hide()
    {
        ThrowIfDisposed();
        _ = NativeMethods.ShowWindow(WindowHandle, NativeConstants.SwHide);
        _backdropWindow.Hide();
        IsVisible = false;
    }

    public void UpdatePlacement(OverlayPosition position, string? displayId)
    {
        ThrowIfDisposed();
        _position = position;
        _displayId = displayId;
        Reposition();
    }

    public void UpdateLayout(OverlaySize sizeInDips, double cornerRadiusInDips)
    {
        ThrowIfDisposed();

        if (!sizeInDips.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeInDips));
        }

        if (!double.IsFinite(cornerRadiusInDips) || cornerRadiusInDips < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cornerRadiusInDips));
        }

        _sizeInDips = sizeInDips;
        _cornerRadiusInDips = cornerRadiusInDips;
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

    public void UpdateSurfaceColor(uint argb)
    {
        ThrowIfDisposed();
        _surfaceArgb = argb;
        UpdateBackdropFromLastPlacement();
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

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _window.Closed -= OnWindowClosed;
        _displayTopology.DisplaysChanged -= OnDisplaysChanged;
        if (_subclassInstalled)
        {
            _ = NativeMethods.RemoveWindowSubclass(WindowHandle, _subclassProcedure, SubclassId);
            _subclassInstalled = false;
        }

        StopOutsideClickMonitoring();
        _backdropWindow.Dispose();
        _disposed = true;
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

        var desiredStyles = WindowStylePolicy.ApplyOverlayStyles(currentStyles.ToInt64());
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
        var dpi = NativeMethods.GetDpiForWindow(WindowHandle);
        if (dpi == 0)
        {
            throw new Win32Exception("Unable to determine overlay DPI.");
        }

        var display = _displayTopology.Find(_displayId) ?? _displayTopology.Primary;
        var workArea = display.WorkArea;
        var placement = _placementCalculator.Calculate(new OverlayLayoutRequest(
            new OverlayWorkArea(workArea.X, workArea.Y, workArea.Width, workArea.Height),
            _sizeInDips,
            dpi,
            _position,
            _options.MarginInDips));

        EnsureSetWindowPos(
            placement.X,
            placement.Y,
            placement.Width,
            placement.Height,
            NativeConstants.HwndTopmost,
            NativeConstants.SwpNoActivate
            | NativeConstants.SwpNoOwnerZOrder);

        ApplyWindowRegion(placement, dpi);
        _lastPlacement = placement;
        _lastDpi = dpi;
        _backdropWindow.Update(
            placement,
            GetCornerRadiusInPixels(dpi),
            GetFeatherInset(dpi) + 1d,
            _surfaceArgb,
            WindowHandle,
            IsVisible);
        LastFailure = null;
    }

    private void ApplyWindowRegion(OverlayPlacement placement, uint dpi)
    {
        var featherInset = GetFeatherInset(dpi);
        var radius = checked((int)Math.Round(
            _cornerRadiusInDips * dpi / 96d,
            MidpointRounding.AwayFromZero)) - featherInset;
        radius = Math.Clamp(radius, 0, Math.Min(
            placement.Width - featherInset * 2,
            placement.Height - featherInset * 2) / 2);
        var diameter = Math.Max(1, radius * 2);
        var region = NativeMethods.CreateRoundRectRgn(
            featherInset,
            featherInset,
            placement.Width - featherInset + 1,
            placement.Height - featherInset + 1,
            diameter,
            diameter);

        if (region == 0)
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Unable to create the overlay transparency region.");
        }

        if (NativeMethods.SetWindowRgn(WindowHandle, region, true) == 0)
        {
            var error = Marshal.GetLastPInvokeError();
            _ = NativeMethods.DeleteObject(region);
            throw new Win32Exception(error, "Unable to apply the overlay transparency region.");
        }

        // SetWindowRgn owns the region after a successful call.
    }

    private void UpdateBackdropFromLastPlacement()
    {
        if (_lastPlacement is not { } placement || _lastDpi == 0)
        {
            return;
        }

        _backdropWindow.Update(
            placement,
            GetCornerRadiusInPixels(_lastDpi),
            GetFeatherInset(_lastDpi) + 1d,
            _surfaceArgb,
            WindowHandle,
            IsVisible);
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
        if (message == NativeConstants.WmMouseActivate)
        {
            return NativeConstants.MaNoActivate;
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
                    GetCornerRadiusInPixels()))
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
            _window.DispatcherQueue.TryEnqueue(TryRefreshAppearanceAndPosition);
        }
        else if (message == NativeConstants.WmNcDestroy)
        {
            Dispose();
        }

        return NativeMethods.DefSubclassProc(windowHandle, message, wParam, lParam);
    }

    private nint LowLevelMouseHandler(int code, nuint message, nint data)
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
                              GetCornerRadiusInPixels());
            if (outside && Interlocked.Exchange(ref _outsideClickPending, 1) == 0)
            {
                _window.DispatcherQueue.TryEnqueue(() =>
                {
                    Interlocked.Exchange(ref _outsideClickPending, 0);
                    OutsidePointerPressed?.Invoke(this, EventArgs.Empty);
                });
            }
        }

        return NativeMethods.CallNextHookEx(_mouseHook, code, message, data);
    }

    private double GetCornerRadiusInPixels()
    {
        var dpi = NativeMethods.GetDpiForWindow(WindowHandle);
        return dpi == 0 ? _cornerRadiusInDips : GetCornerRadiusInPixels(dpi);
    }

    private double GetCornerRadiusInPixels(uint dpi) => _cornerRadiusInDips * dpi / 96d;

    private static int GetFeatherInset(uint dpi) =>
        Math.Max(1, checked((int)Math.Ceiling(dpi / 96d)));

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
        _window.DispatcherQueue.TryEnqueue(TryRefreshAppearanceAndPosition);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
