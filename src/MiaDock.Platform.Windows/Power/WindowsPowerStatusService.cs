using Microsoft.Windows.System.Power;
using MiaDock.Core.Logging;
using MiaDock.Core.Threading;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using MiaDock.Modules.Time.Services;

namespace MiaDock.Platform.Windows.Power;

public sealed class WindowsPowerStatusService : IPowerStatusService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private const int MaximumRetryCount = 3;
    private readonly IUiDispatcher _dispatcher;
    private readonly ISystemResumeService? _resumeService;
    private readonly ILogService? _log;
    private readonly IWindowsPowerStatusReader _reader;
    private readonly IWindowsPowerEventSource _eventSource;
    private readonly object _gate = new();
    private Timer? _retryTimer;
    private int _retryCount;
    private long _generation;
    private bool _started;
    private bool _subscribed;
    private bool _disposed;

    public WindowsPowerStatusService(
        IUiDispatcher dispatcher,
        ISystemResumeService resumeService,
        ILogService? log = null)
        : this(
            dispatcher,
            resumeService,
            new WindowsPowerStatusReader(),
            new WindowsPowerEventSource(),
            log)
    {
    }

    internal WindowsPowerStatusService(
        IUiDispatcher dispatcher,
        ISystemResumeService? resumeService,
        IWindowsPowerStatusReader reader,
        IWindowsPowerEventSource eventSource,
        ILogService? log = null)
    {
        _dispatcher = dispatcher;
        _resumeService = resumeService;
        _reader = reader;
        _eventSource = eventSource;
        _log = log;
    }

    public BatteryStatusSnapshot Current { get; private set; } = BatteryStatusSnapshot.Default;

    public event EventHandler<BatteryStatusSnapshot>? SnapshotChanged;

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        long generation;
        lock (_gate)
        {
            if (_started)
            {
                return ValueTask.CompletedTask;
            }

            _started = true;
            _retryCount = 0;
            generation = ++_generation;
        }
        try
        {
            Subscribe();
        }
        catch (Exception exception)
        {
            HandleReadFailure(exception, generation);
            return ValueTask.CompletedTask;
        }

        Publish(Current with
        {
            State = DeviceServiceState.Starting,
            Availability = BatteryAvailabilityState.Unknown
        }, generation);
        Refresh(generation);
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long generation;
        lock (_gate)
        {
            if (!_started)
            {
                return ValueTask.CompletedTask;
            }

            _started = false;
            generation = ++_generation;
            TryUnsubscribe();
            CancelRetry();
        }

        Publish(BatteryStatusSnapshot.Default, generation, allowStopped: true);
        return ValueTask.CompletedTask;
    }

    private void OnPowerChanged(object? sender, object args) => RefreshCurrentGeneration();

    private void OnSystemResumed(object? sender, EventArgs args) => RefreshCurrentGeneration();

    private void RefreshCurrentGeneration()
    {
        long generation;
        lock (_gate)
        {
            if (!_started || _disposed)
            {
                return;
            }
            generation = _generation;
        }
        Refresh(generation);
    }

    private void Refresh(long generation)
    {
        try
        {
            var snapshot = PowerStatusEvaluator.Evaluate(_reader.Read(), DateTimeOffset.UtcNow);
            lock (_gate)
            {
                if (!IsCurrent(generation))
                {
                    return;
                }
                _retryCount = 0;
                CancelRetry();
            }
            Publish(snapshot, generation);
            _log?.Write(
                TechnicalLogLevel.Information,
                TechnicalEventIds.PowerStatusReady,
                "DeviceStatus",
                "Power status refreshed.",
                properties: new Dictionary<string, object?>
                {
                    ["availability"] = snapshot.Availability.ToString(),
                    ["batteryPresent"] = snapshot.IsBatteryPresent
                });
        }
        catch (Exception exception)
        {
            HandleReadFailure(exception, generation);
        }
    }

    private void HandleReadFailure(Exception exception, long generation)
    {
        var availability = exception switch
        {
            UnauthorizedAccessException => BatteryAvailabilityState.AccessDenied,
            PlatformNotSupportedException or TypeLoadException or MissingMethodException =>
                BatteryAvailabilityState.ApiUnavailable,
            System.Runtime.InteropServices.COMException { HResult: unchecked((int)0x80070005) } =>
                BatteryAvailabilityState.AccessDenied,
            System.Runtime.InteropServices.COMException { HResult: unchecked((int)0x80040154) } =>
                BatteryAvailabilityState.ApiUnavailable,
            _ => BatteryAvailabilityState.TransientError
        };
        var state = availability == BatteryAvailabilityState.TransientError
            ? DeviceServiceState.Faulted
            : DeviceServiceState.Unavailable;
        var failure = Current with { State = state, Availability = availability };
        Publish(failure, generation);

        if (availability == BatteryAvailabilityState.TransientError)
        {
            ScheduleRetry(generation);
        }

        _log?.Write(
            TechnicalLogLevel.Warning,
            TechnicalEventIds.DeviceStatusUnavailable,
            "DeviceStatus",
            "Power status read failed safely.",
            exception,
            new Dictionary<string, object?>
            {
                ["service"] = "power",
                ["availability"] = availability.ToString()
            });
    }

    private void ScheduleRetry(long generation)
    {
        lock (_gate)
        {
            if (!IsCurrent(generation) || _retryCount >= MaximumRetryCount)
            {
                return;
            }
            _retryCount++;
            CancelRetry();
            _retryTimer = new Timer(
                _ => Refresh(generation),
                null,
                RetryDelay,
                Timeout.InfiniteTimeSpan);
        }
    }

    private void Publish(
        BatteryStatusSnapshot snapshot,
        long generation,
        bool allowStopped = false)
    {
        void Apply()
        {
            lock (_gate)
            {
                if (_disposed || generation != _generation || (!allowStopped && !_started))
                {
                    return;
                }
                Current = snapshot;
            }
            SnapshotChanged?.Invoke(this, snapshot);
        }

        if (_dispatcher.HasThreadAccess)
        {
            Apply();
        }
        else
        {
            _dispatcher.TryEnqueue(Apply);
        }
    }

    private void Subscribe()
    {
        _eventSource.Subscribe(OnPowerChanged);
        if (_resumeService is not null)
        {
            _resumeService.Resumed += OnSystemResumed;
        }
        _subscribed = true;
    }

    private void TryUnsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }
        _subscribed = false;
        try
        {
            _eventSource.Unsubscribe(OnPowerChanged);
        }
        catch (Exception exception)
        {
            _log?.Write(
                TechnicalLogLevel.Warning,
                TechnicalEventIds.DeviceStatusUnavailable,
                "DeviceStatus",
                "Power event unsubscription failed safely.",
                exception,
                new Dictionary<string, object?> { ["service"] = "power-events" });
        }
        finally
        {
            if (_resumeService is not null)
            {
                _resumeService.Resumed -= OnSystemResumed;
            }
        }
    }

    private bool IsCurrent(long generation) =>
        _started && !_disposed && generation == _generation;

    private void CancelRetry()
    {
        _retryTimer?.Dispose();
        _retryTimer = null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _generation++;
            if (_started)
            {
                TryUnsubscribe();
            }
            _started = false;
            CancelRetry();
        }
    }

    private sealed class WindowsPowerStatusReader : IWindowsPowerStatusReader
    {
        public PowerStatusReading Read() => new(
            PowerManager.BatteryStatus.ToString(),
            PowerManager.PowerSupplyStatus.ToString(),
            PowerManager.PowerSourceKind.ToString(),
            PowerManager.RemainingChargePercent,
            PowerManager.EnergySaverStatus.ToString().Equals("On", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class WindowsPowerEventSource : IWindowsPowerEventSource
    {
        public void Subscribe(EventHandler<object> handler)
        {
            PowerManager.BatteryStatusChanged += handler;
            PowerManager.PowerSupplyStatusChanged += handler;
            PowerManager.PowerSourceKindChanged += handler;
            PowerManager.RemainingChargePercentChanged += handler;
            PowerManager.EnergySaverStatusChanged += handler;
        }

        public void Unsubscribe(EventHandler<object> handler)
        {
            PowerManager.BatteryStatusChanged -= handler;
            PowerManager.PowerSupplyStatusChanged -= handler;
            PowerManager.PowerSourceKindChanged -= handler;
            PowerManager.RemainingChargePercentChanged -= handler;
            PowerManager.EnergySaverStatusChanged -= handler;
        }
    }
}
