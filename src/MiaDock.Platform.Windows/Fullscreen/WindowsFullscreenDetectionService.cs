using System.ComponentModel;
using System.Runtime.InteropServices;
using MiaDock.Core.Threading;
using MiaDock.Platform.Windows.Interop;

namespace MiaDock.Platform.Windows.Fullscreen;

public sealed class WindowsFullscreenDetectionService : IFullscreenDetectionService
{
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(100);
    private readonly IUiDispatcher _dispatcher;
    private readonly NativeMethods.WinEventProcedure _callback;
    private readonly Timer _refreshTimer;
    private nint _foregroundHook;
    private nint _locationHook;
    private int _refreshDispatchPending;
    private bool _started;
    private bool _disposed;

    public WindowsFullscreenDetectionService(IUiDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _callback = OnWinEvent;
        _refreshTimer = new Timer(OnRefreshTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public FullscreenSnapshot Current { get; private set; } = FullscreenSnapshot.None;

    public Exception? LastFailure { get; private set; }

    public event EventHandler<FullscreenSnapshot>? StateChanged;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return;

        var flags = NativeConstants.WinEventOutOfContext | NativeConstants.WinEventSkipOwnProcess;
        _foregroundHook = NativeMethods.SetWinEventHook(
            NativeConstants.EventSystemForeground, NativeConstants.EventSystemForeground,
            0, _callback, 0, 0, flags);
        _locationHook = NativeMethods.SetWinEventHook(
            NativeConstants.EventObjectLocationChange, NativeConstants.EventObjectLocationChange,
            0, _callback, 0, 0, flags);
        if (_foregroundHook == 0 || _locationHook == 0)
        {
            DisposeHooks();
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Unable to monitor foreground windows.");
        }

        _started = true;
        Refresh();
    }

    public void Refresh()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        try
        {
            var next = EvaluateForegroundWindow();
            LastFailure = null;
            if (next == Current) return;
            Current = next;
            StateChanged?.Invoke(this, next);
        }
        catch (Exception exception)
        {
            LastFailure = exception;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _refreshTimer.Dispose();
        DisposeHooks();
        _disposed = true;
    }

    private FullscreenSnapshot EvaluateForegroundWindow()
    {
        var window = NativeMethods.GetForegroundWindow();
        if (window == 0) return FullscreenSnapshot.None;
        var monitor = NativeMethods.MonitorFromWindow(window, NativeConstants.MonitorDefaultToNearest);
        if (monitor == 0) return FullscreenSnapshot.None;

        var monitorInfo = NativeMonitorInfo.Create();
        if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Unable to read foreground monitor bounds.");
        }

        var windowBounds = GetClientBounds(window);

        _ = NativeMethods.DwmGetWindowAttribute(
            window, NativeConstants.DwmwaCloaked,
            out uint cloaked, sizeof(uint));
        _ = NativeMethods.GetWindowThreadProcessId(window, out var processId);
        var notificationState = 0;
        _ = NativeMethods.SHQueryUserNotificationState(out notificationState);

        var reason = FullscreenClassifier.Classify(new FullscreenEvaluationInput(
            NativeMethods.IsWindowVisible(window),
            NativeMethods.IsIconic(window),
            cloaked != 0,
            processId == Environment.ProcessId,
            window == NativeMethods.GetShellWindow(),
            IsStandardMaximizedWindow(window),
            ToBounds(windowBounds),
            ToBounds(monitorInfo.Monitor),
            notificationState));
        return new FullscreenSnapshot(reason != FullscreenDetectionReason.None, window, monitor, reason);
    }

    private void OnWinEvent(nint hook, uint eventType, nint window, int objectId, int childId, uint eventThread, uint eventTime)
    {
        if (eventType == NativeConstants.EventObjectLocationChange
            && (objectId != NativeConstants.ObjectIdWindow || window != NativeMethods.GetForegroundWindow()))
        {
            return;
        }

        _refreshTimer.Change(RefreshDelay, Timeout.InfiniteTimeSpan);
    }

    private void OnRefreshTimer(object? state)
    {
        if (_disposed ||
            Interlocked.CompareExchange(ref _refreshDispatchPending, 1, 0) != 0)
        {
            return;
        }

        if (!_dispatcher.TryEnqueue(() =>
            {
                Volatile.Write(ref _refreshDispatchPending, 0);
                if (!_disposed)
                {
                    Refresh();
                }
            }))
        {
            Volatile.Write(ref _refreshDispatchPending, 0);
        }
    }

    private void DisposeHooks()
    {
        if (_foregroundHook != 0) _ = NativeMethods.UnhookWinEvent(_foregroundHook);
        if (_locationHook != 0) _ = NativeMethods.UnhookWinEvent(_locationHook);
        _foregroundHook = 0;
        _locationHook = 0;
        _started = false;
    }

    private static PixelBounds ToBounds(NativeRect value) => new(value.Left, value.Top, value.Right, value.Bottom);

    private static bool IsStandardMaximizedWindow(nint windowHandle)
    {
        if (!NativeMethods.IsZoomed(windowHandle))
        {
            return false;
        }

        var style = NativeMethods.GetWindowLongPtr(windowHandle, NativeConstants.GwlStyle).ToInt64();
        return (style & NativeConstants.WsOverlappedWindow) != 0;
    }

    private static NativeRect GetClientBounds(nint windowHandle)
    {
        if (NativeMethods.GetClientRect(windowHandle, out var client))
        {
            var topLeft = new NativePoint { X = client.Left, Y = client.Top };
            var bottomRight = new NativePoint { X = client.Right, Y = client.Bottom };
            if (NativeMethods.ClientToScreen(windowHandle, ref topLeft)
                && NativeMethods.ClientToScreen(windowHandle, ref bottomRight))
            {
                return new NativeRect
                {
                    Left = topLeft.X,
                    Top = topLeft.Y,
                    Right = bottomRight.X,
                    Bottom = bottomRight.Y
                };
            }
        }

        NativeRect extendedBounds;
        if (NativeMethods.DwmGetWindowAttribute(
                windowHandle, NativeConstants.DwmwaExtendedFrameBounds,
                out extendedBounds, Marshal.SizeOf<NativeRect>()) >= 0
            || NativeMethods.GetWindowRect(windowHandle, out extendedBounds))
        {
            return extendedBounds;
        }

        throw new Win32Exception(Marshal.GetLastPInvokeError(), "Unable to read foreground window bounds.");
    }
}
