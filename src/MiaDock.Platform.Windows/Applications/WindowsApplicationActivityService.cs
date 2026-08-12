using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using MiaDock.Core.Focus;
using MiaDock.Core.Threading;
using MiaDock.Platform.Windows.Interop;

namespace MiaDock.Platform.Windows.Applications;

public sealed class WindowsApplicationActivityService : IApplicationActivityService
{
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(150);

    private readonly object _gate = new();
    private readonly IUiDispatcher _dispatcher;
    private readonly NativeMethods.WinEventProcedure _foregroundCallback;
    private readonly Timer _refreshTimer;
    private ManagementEventWatcher? _processStartWatcher;
    private ManagementEventWatcher? _processStopWatcher;
    private nint _foregroundHook;
    private bool _processMonitoringAvailable;
    private bool _started;
    private bool _disposed;
    private int _refreshPending;

    public WindowsApplicationActivityService(IUiDispatcher dispatcher)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _foregroundCallback = OnForegroundChanged;
        _refreshTimer = new Timer(
            OnRefreshTimer,
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
    }

    public ApplicationActivitySnapshot Current { get; private set; } =
        ApplicationActivitySnapshot.Empty;

    public Exception? LastFailure { get; private set; }

    public event EventHandler<ApplicationActivitySnapshot>? ActivityChanged;

    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_started)
            {
                return;
            }

            _started = true;
            var flags = NativeConstants.WinEventOutOfContext |
                        NativeConstants.WinEventSkipOwnProcess;
            _foregroundHook = NativeMethods.SetWinEventHook(
                NativeConstants.EventSystemForeground,
                NativeConstants.EventSystemForeground,
                0,
                _foregroundCallback,
                0,
                0,
                flags);
            StartProcessWatchersLocked();
        }

        Refresh();
    }

    public void Refresh()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ApplicationActivitySnapshot next;
        try
        {
            next = CaptureSnapshot();
            LastFailure = null;
        }
        catch (Exception exception)
        {
            LastFailure = exception;
            next = Current with
            {
                ForegroundTarget = ResolveForegroundTarget(),
                IsProcessMonitoringAvailable = _processMonitoringAvailable
            };
        }

        if (SnapshotsEqual(Current, next))
        {
            return;
        }

        Current = next;
        ActivityChanged?.Invoke(this, next);
    }

    public void Dispose()
    {
        ManagementEventWatcher? processStartWatcher;
        ManagementEventWatcher? processStopWatcher;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _started = false;
            _refreshTimer.Dispose();
            if (_foregroundHook != 0)
            {
                _ = NativeMethods.UnhookWinEvent(_foregroundHook);
                _foregroundHook = 0;
            }

            processStartWatcher = _processStartWatcher;
            processStopWatcher = _processStopWatcher;
            _processStartWatcher = null;
            _processStopWatcher = null;
        }

        DisposeWatcher(processStartWatcher);
        DisposeWatcher(processStopWatcher);
    }

    private ApplicationActivitySnapshot CaptureSnapshot()
    {
        var running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var available = new Dictionary<string, FocusApplicationInfo>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id == Environment.ProcessId)
                    {
                        continue;
                    }

                    var target = FocusApplicationTarget.Normalize(process.ProcessName);
                    if (target.Length == 0)
                    {
                        continue;
                    }

                    running.Add(target);
                    if (process.MainWindowHandle != 0)
                    {
                        available.TryAdd(
                            target,
                            new FocusApplicationInfo(
                                target,
                                Path.GetFileNameWithoutExtension(target)));
                    }
                }
                catch (Exception exception) when (
                    exception is InvalidOperationException or
                    System.ComponentModel.Win32Exception or
                    NotSupportedException)
                {
                }
            }
        }

        return new ApplicationActivitySnapshot(
            ResolveForegroundTarget(),
            running,
            available.Values
                .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            _processMonitoringAvailable);
    }

    private string? ResolveForegroundTarget()
    {
        try
        {
            var window = NativeMethods.GetForegroundWindow();
            if (window == 0)
            {
                return null;
            }

            _ = NativeMethods.GetWindowThreadProcessId(window, out var processId);
            if (processId == 0 || processId == Environment.ProcessId)
            {
                return null;
            }

            using var process = Process.GetProcessById(checked((int)processId));
            var target = FocusApplicationTarget.Normalize(process.ProcessName);
            return target.Length == 0 ? null : target;
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            InvalidOperationException or
            System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private void StartProcessWatchersLocked()
    {
        try
        {
            _processStartWatcher =
                new ManagementEventWatcher("SELECT * FROM Win32_ProcessStartTrace");
            _processStopWatcher =
                new ManagementEventWatcher("SELECT * FROM Win32_ProcessStopTrace");
            _processStartWatcher.EventArrived += OnProcessChanged;
            _processStopWatcher.EventArrived += OnProcessChanged;
            _processStartWatcher.Start();
            _processStopWatcher.Start();
            _processMonitoringAvailable = true;
        }
        catch (Exception exception) when (
            exception is ManagementException or
            UnauthorizedAccessException or
            PlatformNotSupportedException)
        {
            LastFailure = exception;
            _processMonitoringAvailable = false;
            DisposeWatcher(ref _processStartWatcher);
            DisposeWatcher(ref _processStopWatcher);
        }
    }

    private void OnForegroundChanged(
        nint hook,
        uint eventType,
        nint window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        try
        {
            RequestRefresh();
        }
        catch (Exception exception)
        {
            // The hook runs on a native stack, where an escaping managed
            // exception fails fast instead of surfacing as a handled error.
            LastFailure = exception;
        }
    }

    private void OnProcessChanged(object sender, EventArrivedEventArgs args) =>
        RequestRefresh();

    private void RequestRefresh()
    {
        lock (_gate)
        {
            if (_disposed || !_started)
            {
                return;
            }

            _refreshTimer.Change(RefreshDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnRefreshTimer(object? state)
    {
        if (_disposed ||
            Interlocked.CompareExchange(ref _refreshPending, 1, 0) != 0)
        {
            return;
        }

        if (!_dispatcher.TryEnqueue(() =>
            {
                Volatile.Write(ref _refreshPending, 0);
                if (!_disposed)
                {
                    Refresh();
                }
            }))
        {
            Volatile.Write(ref _refreshPending, 0);
        }
    }

    private static bool SnapshotsEqual(
        ApplicationActivitySnapshot left,
        ApplicationActivitySnapshot right) =>
        string.Equals(
            left.ForegroundTarget,
            right.ForegroundTarget,
            StringComparison.OrdinalIgnoreCase) &&
        left.IsProcessMonitoringAvailable == right.IsProcessMonitoringAvailable &&
        left.RunningTargets.SetEquals(right.RunningTargets) &&
        left.AvailableApplications.SequenceEqual(right.AvailableApplications);

    private void DisposeWatcher(ref ManagementEventWatcher? watcher)
    {
        var captured = watcher;
        watcher = null;
        DisposeWatcher(captured);
    }

    private void DisposeWatcher(ManagementEventWatcher? watcher)
    {
        if (watcher is null)
        {
            return;
        }

        try
        {
            watcher.EventArrived -= OnProcessChanged;
            watcher.Stop();
        }
        catch (Exception exception) when (
            exception is ManagementException or
            InvalidOperationException or
            COMException)
        {
        }
        finally
        {
            watcher.Dispose();
        }
    }
}

internal static class ReadOnlySetExtensions
{
    public static bool SetEquals<T>(
        this IReadOnlySet<T> left,
        IReadOnlySet<T> right) =>
        left.Count == right.Count && left.All(right.Contains);
}
