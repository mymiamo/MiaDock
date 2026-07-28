using MiaDock.Modules.Time.Models;
using MiaDock.Modules.Time.Persistence;

namespace MiaDock.Modules.Time.Services;

public sealed class TimeToolsService : ITimeToolsService
{
    private static readonly TimeSpan MaximumTimerDuration = TimeSpan.FromHours(99);
    private readonly object _gate = new();
    private readonly ITimerStateStore _store;
    private readonly ISystemResumeService? _resumeService;
    private readonly ITimerAlarmPlayer? _alarmPlayer;
    private readonly TimeProvider _timeProvider;
    private Timer? _ticker;
    private long _timerAnchorTimestamp;
    private TimeSpan _timerAnchorRemaining;
    private long _stopwatchAnchorTimestamp;
    private DateTimeOffset _stopwatchAnchorUtc;
    private TimeSpan _stopwatchAccumulated;
    private bool _completionPending;
    private bool _initialized;
    private bool _disposed;

    public TimeToolsService(
        ITimerStateStore store,
        ISystemResumeService? resumeService = null,
        TimeProvider? timeProvider = null,
        ITimerAlarmPlayer? alarmPlayer = null)
    {
        _store = store;
        _resumeService = resumeService;
        _alarmPlayer = alarmPlayer;
        _timeProvider = timeProvider ?? TimeProvider.System;
        if (_resumeService is not null)
        {
            _resumeService.Resumed += OnSystemResumed;
        }
    }

    public TimeToolsSnapshot Current { get; private set; } = TimeToolsSnapshot.Default;

    public event EventHandler<TimeToolsSnapshot>? SnapshotChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        var persisted = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var playRestoredAlarm = false;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_initialized)
            {
                return;
            }

            RestoreLocked(persisted);
            playRestoredAlarm = Current.TimerState == TimerRunState.Completed && _completionPending;
            _initialized = true;
            _resumeService?.Start();
            UpdateTickerLocked();
        }

        Publish(Current);
        if (playRestoredAlarm)
        {
            _alarmPlayer?.Play();
        }
        await PersistAsync().ConfigureAwait(false);
    }

    public bool StartTimer(TimeSpan duration)
    {
        if (duration < TimeSpan.FromSeconds(1) || duration > MaximumTimerDuration)
        {
            return false;
        }

        _alarmPlayer?.Stop();
        TimeToolsSnapshot snapshot;
        lock (_gate)
        {
            ThrowIfUnavailable();
            _completionPending = false;
            _timerAnchorTimestamp = _timeProvider.GetTimestamp();
            _timerAnchorRemaining = duration;
            snapshot = Current = Current with
            {
                TimerState = TimerRunState.Running,
                TimerDuration = duration,
                TimerRemaining = duration,
                TimerTargetUtc = _timeProvider.GetUtcNow().Add(duration)
            };
            UpdateTickerLocked();
        }

        Publish(snapshot);
        _ = PersistAsync();
        return true;
    }

    public bool PauseTimer()
    {
        TimeToolsSnapshot snapshot;
        lock (_gate)
        {
            ThrowIfUnavailable();
            if (Current.TimerState != TimerRunState.Running)
            {
                return false;
            }

            var remaining = CalculateTimerRemainingLocked();
            _timerAnchorRemaining = remaining;
            snapshot = Current = Current with
            {
                TimerState = TimerRunState.Paused,
                TimerRemaining = remaining,
                TimerTargetUtc = null
            };
            UpdateTickerLocked();
        }

        Publish(snapshot);
        _ = PersistAsync();
        return true;
    }

    public bool ResumeTimer()
    {
        TimeToolsSnapshot snapshot;
        lock (_gate)
        {
            ThrowIfUnavailable();
            if (Current.TimerState != TimerRunState.Paused || Current.TimerRemaining <= TimeSpan.Zero)
            {
                return false;
            }

            _timerAnchorRemaining = Current.TimerRemaining;
            _timerAnchorTimestamp = _timeProvider.GetTimestamp();
            snapshot = Current = Current with
            {
                TimerState = TimerRunState.Running,
                TimerTargetUtc = _timeProvider.GetUtcNow().Add(Current.TimerRemaining)
            };
            UpdateTickerLocked();
        }

        Publish(snapshot);
        _ = PersistAsync();
        return true;
    }

    public bool CancelTimer()
    {
        TimeToolsSnapshot snapshot;
        lock (_gate)
        {
            ThrowIfUnavailable();
            if (Current.TimerState == TimerRunState.Idle)
            {
                return false;
            }

            _completionPending = false;
            snapshot = Current = Current with
            {
                TimerState = TimerRunState.Idle,
                TimerDuration = TimeSpan.Zero,
                TimerRemaining = TimeSpan.Zero,
                TimerTargetUtc = null
            };
            UpdateTickerLocked();
        }

        _alarmPlayer?.Stop();
        Publish(snapshot);
        _ = PersistAsync();
        return true;
    }

    public bool ConsumePendingCompletion()
    {
        lock (_gate)
        {
            if (!_completionPending)
            {
                return false;
            }

            _completionPending = false;
        }

        _ = PersistAsync();
        return true;
    }

    public bool StartStopwatch()
    {
        TimeToolsSnapshot snapshot;
        lock (_gate)
        {
            ThrowIfUnavailable();
            if (Current.IsStopwatchRunning)
            {
                return false;
            }

            _stopwatchAccumulated = Current.StopwatchElapsed;
            _stopwatchAnchorTimestamp = _timeProvider.GetTimestamp();
            _stopwatchAnchorUtc = _timeProvider.GetUtcNow();
            snapshot = Current = Current with { IsStopwatchRunning = true };
            UpdateTickerLocked();
        }

        Publish(snapshot);
        return true;
    }

    public bool PauseStopwatch()
    {
        TimeToolsSnapshot snapshot;
        lock (_gate)
        {
            ThrowIfUnavailable();
            if (!Current.IsStopwatchRunning)
            {
                return false;
            }

            _stopwatchAccumulated = CalculateStopwatchElapsedLocked();
            snapshot = Current = Current with
            {
                IsStopwatchRunning = false,
                StopwatchElapsed = _stopwatchAccumulated
            };
            UpdateTickerLocked();
        }

        Publish(snapshot);
        return true;
    }

    public bool AddLap()
    {
        TimeToolsSnapshot snapshot;
        lock (_gate)
        {
            ThrowIfUnavailable();
            if (!Current.IsStopwatchRunning)
            {
                return false;
            }

            var elapsed = CalculateStopwatchElapsedLocked();
            var laps = Current.Laps.Append(elapsed).TakeLast(100).ToArray();
            snapshot = Current = Current with { StopwatchElapsed = elapsed, Laps = laps };
        }

        Publish(snapshot);
        return true;
    }

    public bool ResetStopwatch()
    {
        TimeToolsSnapshot snapshot;
        lock (_gate)
        {
            ThrowIfUnavailable();
            if (Current.IsStopwatchRunning ||
                (Current.StopwatchElapsed == TimeSpan.Zero && Current.Laps.Count == 0))
            {
                return false;
            }

            _stopwatchAccumulated = TimeSpan.Zero;
            snapshot = Current = Current with
            {
                StopwatchElapsed = TimeSpan.Zero,
                Laps = Array.Empty<TimeSpan>()
            };
        }

        Publish(snapshot);
        return true;
    }

    private void OnTick(object? state)
    {
        TimeToolsSnapshot? snapshot = null;
        var completed = false;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var updated = Current;
            if (Current.TimerState == TimerRunState.Running)
            {
                var remaining = CalculateTimerRemainingLocked();
                if (remaining <= TimeSpan.Zero)
                {
                    _completionPending = true;
                    completed = true;
                    updated = updated with
                    {
                        TimerState = TimerRunState.Completed,
                        TimerRemaining = TimeSpan.Zero,
                        TimerTargetUtc = null
                    };
                }
                else
                {
                    updated = updated with { TimerRemaining = remaining };
                }
            }

            if (Current.IsStopwatchRunning)
            {
                updated = updated with { StopwatchElapsed = CalculateStopwatchElapsedLocked() };
            }

            if (updated != Current)
            {
                snapshot = Current = updated;
            }

            if (completed)
            {
                UpdateTickerLocked();
            }
        }

        if (snapshot is not null)
        {
            Publish(snapshot);
        }

        if (completed)
        {
            _alarmPlayer?.Play();
            _ = PersistAsync();
        }
    }

    private void OnSystemResumed(object? sender, EventArgs args)
    {
        TimeToolsSnapshot snapshot;
        var completed = false;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            var updated = Current;
            if (Current.TimerState == TimerRunState.Running && Current.TimerTargetUtc is { } target)
            {
                var remaining = target - _timeProvider.GetUtcNow();
                if (remaining <= TimeSpan.Zero)
                {
                    _completionPending = true;
                    completed = true;
                    updated = updated with
                    {
                        TimerState = TimerRunState.Completed,
                        TimerRemaining = TimeSpan.Zero,
                        TimerTargetUtc = null
                    };
                }
                else
                {
                    _timerAnchorRemaining = remaining;
                    _timerAnchorTimestamp = _timeProvider.GetTimestamp();
                    updated = updated with { TimerRemaining = remaining };
                }
            }

            if (Current.IsStopwatchRunning)
            {
                _stopwatchAccumulated += _timeProvider.GetUtcNow() - _stopwatchAnchorUtc;
                _stopwatchAnchorTimestamp = _timeProvider.GetTimestamp();
                _stopwatchAnchorUtc = _timeProvider.GetUtcNow();
                updated = updated with { StopwatchElapsed = _stopwatchAccumulated };
            }

            snapshot = Current = updated;
            UpdateTickerLocked();
        }

        Publish(snapshot);
        if (completed)
        {
            _alarmPlayer?.Play();
        }
        _ = PersistAsync();
    }

    private void RestoreLocked(TimerPersistentState? state)
    {
        if (state is null || state.SchemaVersion != TimerPersistentState.CurrentSchemaVersion)
        {
            return;
        }

        var duration = SafeTimeSpan(state.DurationTicks);
        var remaining = SafeTimeSpan(state.RemainingTicks);
        if (state.State == TimerRunState.Completed && state.CompletionPending)
        {
            _completionPending = true;
            Current = Current with
            {
                TimerState = TimerRunState.Completed,
                TimerDuration = duration,
                TimerRemaining = TimeSpan.Zero
            };
            return;
        }

        if (state.State == TimerRunState.Running && state.TargetUtc is { } target)
        {
            remaining = target - _timeProvider.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                _completionPending = true;
                Current = Current with
                {
                    TimerState = TimerRunState.Completed,
                    TimerDuration = duration,
                    TimerRemaining = TimeSpan.Zero
                };
                return;
            }

            _timerAnchorTimestamp = _timeProvider.GetTimestamp();
            _timerAnchorRemaining = remaining;
            Current = Current with
            {
                TimerState = TimerRunState.Running,
                TimerDuration = duration,
                TimerRemaining = remaining,
                TimerTargetUtc = target
            };
            return;
        }

        if (state.State == TimerRunState.Paused && remaining > TimeSpan.Zero)
        {
            _timerAnchorRemaining = remaining;
            Current = Current with
            {
                TimerState = TimerRunState.Paused,
                TimerDuration = duration,
                TimerRemaining = remaining
            };
        }
    }

    private TimeSpan CalculateTimerRemainingLocked()
    {
        var elapsed = _timeProvider.GetElapsedTime(_timerAnchorTimestamp, _timeProvider.GetTimestamp());
        return _timerAnchorRemaining > elapsed ? _timerAnchorRemaining - elapsed : TimeSpan.Zero;
    }

    private TimeSpan CalculateStopwatchElapsedLocked() =>
        _stopwatchAccumulated + _timeProvider.GetElapsedTime(
            _stopwatchAnchorTimestamp, _timeProvider.GetTimestamp());

    private void UpdateTickerLocked()
    {
        var required = Current.TimerState == TimerRunState.Running || Current.IsStopwatchRunning;
        if (!required)
        {
            _ticker?.Dispose();
            _ticker = null;
            return;
        }

        _ticker ??= new Timer(OnTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    }

    private async Task PersistAsync()
    {
        TimerPersistentState state;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            state = new TimerPersistentState(
                TimerPersistentState.CurrentSchemaVersion,
                Current.TimerState,
                Current.TimerDuration.Ticks,
                Current.TimerRemaining.Ticks,
                Current.TimerTargetUtc,
                _completionPending);
        }

        try
        {
            await _store.SaveAsync(state).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }

    private void Publish(TimeToolsSnapshot snapshot) => SnapshotChanged?.Invoke(this, snapshot);

    private static TimeSpan SafeTimeSpan(long ticks) =>
        ticks is > 0 and <= TimeSpan.TicksPerDay * 5 ? TimeSpan.FromTicks(ticks) : TimeSpan.Zero;

    private void ThrowIfUnavailable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
        {
            throw new InvalidOperationException("Time tools must be initialized first.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
        }

        await PersistAsync().ConfigureAwait(false);
        _alarmPlayer?.Stop();
        lock (_gate)
        {
            _disposed = true;
            _ticker?.Dispose();
            _ticker = null;
        }

        if (_resumeService is not null)
        {
            _resumeService.Resumed -= OnSystemResumed;
            _resumeService.Dispose();
        }
    }
}
