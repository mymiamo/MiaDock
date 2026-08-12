using System.ComponentModel;
using System.Runtime.InteropServices;
using MiaDock.Core.Logging;
using MiaDock.Core.Threading;
using MiaDock.Platform.Windows.Interop;

namespace MiaDock.Platform.Windows.Fullscreen;

public sealed class WindowsFullscreenDetectionService : IFullscreenDetectionService
{
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan FullscreenRecoveryInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan FailureLogInterval = TimeSpan.FromMinutes(1);
    private readonly object _stateGate = new();
    private readonly IUiDispatcher _dispatcher;
    private readonly Func<FullscreenSnapshot> _evaluateForegroundWindow;
    private readonly ILogService? _log;
    private readonly TimeProvider _timeProvider;
    private readonly ExclusiveFullscreenSignalTracker _exclusiveSignalTracker = new();
    private readonly NativeMethods.WinEventProcedure _callback;
    private readonly ITimer _refreshTimer;
    private readonly ITimer _fullscreenRecoveryTimer;
    private readonly TimeSpan _fullscreenRecoveryInterval;
    private nint _foregroundHook;
    private nint _locationHook;
    private nint _lifecycleHook;
    private nint _minimizeHook;
    private FullscreenSnapshot _current = FullscreenSnapshot.None;
    private FullscreenSnapshot _observed = FullscreenSnapshot.None;
    private FullscreenRefreshSource _observedSource;
    private Exception? _lastFailure;
    private DateTimeOffset _lastFailureLogUtc = DateTimeOffset.MinValue;
    private int _refreshWorkPending;
    private int _refreshRequested;
    private int _requestedSource;
    private int _publishDispatchPending;
    private bool _fullscreenRecoveryEnabled;
    private bool _started;
    private volatile bool _disposed;

    public WindowsFullscreenDetectionService(IUiDispatcher dispatcher, ILogService? log = null)
        : this(dispatcher, null, TimeProvider.System, FullscreenRecoveryInterval, log)
    {
    }

    internal WindowsFullscreenDetectionService(
        IUiDispatcher dispatcher,
        Func<FullscreenSnapshot>? evaluateForegroundWindow,
        TimeProvider timeProvider,
        TimeSpan fullscreenRecoveryInterval,
        ILogService? log = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (fullscreenRecoveryInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(fullscreenRecoveryInterval));
        }

        _evaluateForegroundWindow = evaluateForegroundWindow ?? EvaluateForegroundWindow;
        _log = log;
        _timeProvider = timeProvider;
        _fullscreenRecoveryInterval = fullscreenRecoveryInterval;
        _callback = OnWinEvent;
        _refreshTimer = timeProvider.CreateTimer(
            OnEventRefreshTimer,
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        _fullscreenRecoveryTimer = timeProvider.CreateTimer(
            OnRecoveryRefreshTimer,
            null,
            Timeout.InfiniteTimeSpan,
            fullscreenRecoveryInterval);
    }

    public FullscreenSnapshot Current
    {
        get
        {
            lock (_stateGate)
            {
                return _current;
            }
        }
    }

    public Exception? LastFailure
    {
        get
        {
            lock (_stateGate)
            {
                return _lastFailure;
            }
        }
    }

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

        _lifecycleHook = NativeMethods.SetWinEventHook(
            NativeConstants.EventObjectDestroy, NativeConstants.EventObjectHide,
            0, _callback, 0, 0, flags);
        if (_lifecycleHook == 0)
        {
            LogHookUnavailable("window-lifecycle");
        }

        _minimizeHook = NativeMethods.SetWinEventHook(
            NativeConstants.EventSystemMinimizeStart, NativeConstants.EventSystemMinimizeEnd,
            0, _callback, 0, 0, flags);
        if (_minimizeHook == 0)
        {
            LogHookUnavailable("window-minimize");
        }

        _started = true;
        Refresh();
    }

    public void Refresh()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        RequestRefresh(FullscreenRefreshSource.Manual);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _refreshTimer.Dispose();
        _fullscreenRecoveryTimer.Dispose();
        DisposeHooks();
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
        notificationState = _exclusiveSignalTracker.Filter(window, notificationState);

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
        try
        {
            ObserveWinEvent(eventType, window, objectId);
        }
        catch (Exception exception)
        {
            // WinEvent hooks call back on a native stack. An escaping managed
            // exception fails fast and kills the process before it can be
            // logged, so shutdown races are recorded and swallowed here.
            lock (_stateGate)
            {
                _lastFailure = exception;
            }
        }
    }

    private void ObserveWinEvent(uint eventType, nint window, int objectId)
    {
        if (_disposed)
        {
            return;
        }

        var foregroundWindow = NativeMethods.GetForegroundWindow();
        var trackedWindow = Current.WindowHandle;
        var relevant = eventType switch
        {
            NativeConstants.EventSystemForeground => true,
            NativeConstants.EventObjectLocationChange =>
                objectId == NativeConstants.ObjectIdWindow && window == foregroundWindow,
            NativeConstants.EventObjectDestroy or NativeConstants.EventObjectHide =>
                objectId == NativeConstants.ObjectIdWindow &&
                (window == foregroundWindow || window == trackedWindow),
            NativeConstants.EventObjectShow =>
                objectId == NativeConstants.ObjectIdWindow && window == foregroundWindow,
            NativeConstants.EventSystemMinimizeStart or NativeConstants.EventSystemMinimizeEnd =>
                window == foregroundWindow || window == trackedWindow,
            _ => false
        };
        if (relevant)
        {
            _refreshTimer.Change(RefreshDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnEventRefreshTimer(object? state) =>
        RequestRefresh(FullscreenRefreshSource.WindowEvent);

    private void OnRecoveryRefreshTimer(object? state) =>
        RequestRefresh(FullscreenRefreshSource.Recovery);

    private void RequestRefresh(FullscreenRefreshSource source)
    {
        if (_disposed)
        {
            return;
        }

        Interlocked.Exchange(ref _requestedSource, (int)source);
        Volatile.Write(ref _refreshRequested, 1);
        if (Interlocked.CompareExchange(ref _refreshWorkPending, 1, 0) != 0)
        {
            return;
        }

        try
        {
            do
            {
                Volatile.Write(ref _refreshRequested, 0);
                EvaluateAndPublish((FullscreenRefreshSource)Volatile.Read(ref _requestedSource));
            }
            while (!_disposed && Volatile.Read(ref _refreshRequested) != 0);
        }
        finally
        {
            Volatile.Write(ref _refreshWorkPending, 0);
        }

        if (!_disposed && Volatile.Read(ref _refreshRequested) != 0)
        {
            RequestRefresh((FullscreenRefreshSource)Volatile.Read(ref _requestedSource));
        }
    }

    private void EvaluateAndPublish(FullscreenRefreshSource source)
    {
        try
        {
            var next = _evaluateForegroundWindow();
            lock (_stateGate)
            {
                _lastFailure = null;
                _observed = next;
                _observedSource = source;
            }

            UpdateFullscreenRecovery(next.IsFullscreen);
            RequestPublish();
        }
        catch (Exception exception)
        {
            lock (_stateGate)
            {
                _lastFailure = exception;
            }

            LogDetectionFailure(exception, source);
        }
    }

    private void RequestPublish()
    {
        if (_disposed || ObservedMatchesCurrent())
        {
            return;
        }

        if (_dispatcher.HasThreadAccess)
        {
            PublishObserved();
            return;
        }

        if (Interlocked.CompareExchange(ref _publishDispatchPending, 1, 0) != 0)
        {
            return;
        }

        if (!_dispatcher.TryEnqueue(() =>
            {
                Volatile.Write(ref _publishDispatchPending, 0);
                if (!_disposed)
                {
                    PublishObserved();
                }
            }))
        {
            Volatile.Write(ref _publishDispatchPending, 0);
            if (!_disposed)
            {
                _refreshTimer.Change(RefreshDelay, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void PublishObserved()
    {
        while (!_disposed)
        {
            FullscreenSnapshot next;
            FullscreenRefreshSource source;
            lock (_stateGate)
            {
                if (_observed == _current)
                {
                    return;
                }

                next = _observed;
                source = _observedSource;
                _current = next;
            }

            _log?.Write(
                TechnicalLogLevel.Information,
                TechnicalEventIds.FullscreenStateChanged,
                "Fullscreen",
                "Fullscreen state changed.",
                properties: new Dictionary<string, object?>
                {
                    ["isFullscreen"] = next.IsFullscreen,
                    ["reason"] = next.Reason.ToString(),
                    ["source"] = source.ToString()
                });
            StateChanged?.Invoke(this, next);
        }
    }

    private bool ObservedMatchesCurrent()
    {
        lock (_stateGate)
        {
            return _observed == _current;
        }
    }

    private void UpdateFullscreenRecovery(bool enabled)
    {
        lock (_stateGate)
        {
            if (_fullscreenRecoveryEnabled == enabled)
            {
                return;
            }

            _fullscreenRecoveryEnabled = enabled;
        }

        _fullscreenRecoveryTimer.Change(
            enabled ? _fullscreenRecoveryInterval : Timeout.InfiniteTimeSpan,
            enabled ? _fullscreenRecoveryInterval : Timeout.InfiniteTimeSpan);
    }

    private void LogDetectionFailure(Exception exception, FullscreenRefreshSource source)
    {
        var now = _timeProvider.GetUtcNow();
        lock (_stateGate)
        {
            if (now - _lastFailureLogUtc < FailureLogInterval)
            {
                return;
            }

            _lastFailureLogUtc = now;
        }

        _log?.Write(
            TechnicalLogLevel.Warning,
            TechnicalEventIds.FullscreenDetectionFailed,
            "Fullscreen",
            "Fullscreen detection failed and will retry.",
            exception,
            new Dictionary<string, object?> { ["source"] = source.ToString() });
    }

    private void LogHookUnavailable(string hook)
    {
        _log?.Write(
            TechnicalLogLevel.Warning,
            TechnicalEventIds.FullscreenHookUnavailable,
            "Fullscreen",
            "An optional fullscreen window hook is unavailable; recovery polling remains active.",
            properties: new Dictionary<string, object?> { ["hook"] = hook });
    }

    private void DisposeHooks()
    {
        if (_foregroundHook != 0) _ = NativeMethods.UnhookWinEvent(_foregroundHook);
        if (_locationHook != 0) _ = NativeMethods.UnhookWinEvent(_locationHook);
        if (_lifecycleHook != 0) _ = NativeMethods.UnhookWinEvent(_lifecycleHook);
        if (_minimizeHook != 0) _ = NativeMethods.UnhookWinEvent(_minimizeHook);
        _foregroundHook = 0;
        _locationHook = 0;
        _lifecycleHook = 0;
        _minimizeHook = 0;
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

    private enum FullscreenRefreshSource
    {
        Manual,
        WindowEvent,
        Recovery
    }
}
