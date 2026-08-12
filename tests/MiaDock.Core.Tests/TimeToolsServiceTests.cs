using MiaDock.Modules.Time.Models;
using MiaDock.Modules.Time.Persistence;
using MiaDock.Modules.Time.Services;
using MiaDock.Modules.Time.ViewModels;
using MiaDock.Core.Logging;
using MiaDock.Core.Threading;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class TimeToolsServiceTests
{
    [TestMethod]
    public void RapidTimerSnapshots_QueueOneUiCallbackAndApplyNewestSnapshot()
    {
        var service = new PublishingTimeToolsService();
        var dispatcher = new QueuedDispatcher();
        using var viewModel = new TimeToolsViewModel(service, dispatcher);

        for (var index = 1; index <= 50_000; index++)
        {
            service.Publish(service.Current with
            {
                StopwatchElapsed = TimeSpan.FromMilliseconds(index)
            });
        }

        Assert.AreEqual(1, dispatcher.PendingCount);
        dispatcher.RunQueued();
        Assert.AreEqual(TimeSpan.FromSeconds(50), viewModel.Current.StopwatchElapsed);
        Assert.AreEqual(0, dispatcher.PendingCount);
    }

    [TestMethod]
    public void StopwatchTick_PreservesSelectionAndRaisesOnlyTimeProperties()
    {
        var service = new PublishingTimeToolsService();
        using var viewModel = new TimeToolsViewModel(service);
        viewModel.SelectedToolIndex = 1;
        service.Publish(service.Current with
        {
            IsStopwatchRunning = true,
            StopwatchElapsed = TimeSpan.FromSeconds(1)
        });
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        service.Publish(service.Current with { StopwatchElapsed = TimeSpan.FromSeconds(1.1) });

        Assert.AreEqual(1, viewModel.SelectedToolIndex);
        CollectionAssert.Contains(changed, nameof(TimeToolsViewModel.Current));
        CollectionAssert.Contains(changed, nameof(TimeToolsViewModel.StopwatchText));
        CollectionAssert.DoesNotContain(changed, nameof(TimeToolsViewModel.CompactTimeText));
        CollectionAssert.DoesNotContain(changed, nameof(TimeToolsViewModel.StopwatchPrimaryText));
        CollectionAssert.DoesNotContain(changed, nameof(TimeToolsViewModel.LapTexts));
        CollectionAssert.DoesNotContain(changed, nameof(TimeToolsViewModel.TimerStatusText));
    }

    [TestMethod]
    public void SelectedTool_InvalidValueFallsBackToTimer()
    {
        using var viewModel = new TimeToolsViewModel(new PublishingTimeToolsService());
        viewModel.SelectedToolIndex = 1;

        viewModel.SelectedToolIndex = 42;

        Assert.AreEqual(0, viewModel.SelectedToolIndex);
    }

    [TestMethod]
    public void CustomDuration_NormalizesNonFiniteValuesAndClampsToServiceMaximum()
    {
        var service = new PublishingTimeToolsService();
        using var viewModel = new TimeToolsViewModel(service)
        {
            CustomHours = double.NaN,
            CustomMinutes = 5.9,
            CustomSeconds = double.PositiveInfinity
        };

        viewModel.TimerPrimaryCommand.Execute(null);
        Assert.AreEqual(TimeSpan.FromMinutes(5), service.LastStartedDuration);

        viewModel.CustomHours = 99;
        viewModel.CustomMinutes = 59;
        viewModel.CustomSeconds = 59;
        viewModel.TimerPrimaryCommand.Execute(null);
        Assert.AreEqual(TimeSpan.FromHours(99), service.LastStartedDuration);
    }

    [TestMethod]
    public async Task Timer_UsesMonotonicElapsedTimeWhenPaused()
    {
        var time = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-22T10:00:00Z"));
        await using var service = new TimeToolsService(new MemoryStore(), timeProvider: time);
        await service.InitializeAsync();

        Assert.IsTrue(service.StartTimer(TimeSpan.FromSeconds(10)));
        time.Advance(TimeSpan.FromSeconds(4));
        Assert.IsTrue(service.PauseTimer());

        Assert.AreEqual(TimeSpan.FromSeconds(6), service.Current.TimerRemaining);
        Assert.AreEqual(TimerRunState.Paused, service.Current.TimerState);
    }

    private sealed class QueuedDispatcher : IUiDispatcher
    {
        private readonly Queue<Action> _callbacks = new();

        public bool HasThreadAccess => false;
        public int PendingCount => _callbacks.Count;

        public bool TryEnqueue(Action callback)
        {
            _callbacks.Enqueue(callback);
            return true;
        }

        public void RunQueued()
        {
            while (_callbacks.TryDequeue(out var callback))
            {
                callback();
            }
        }
    }

    private sealed class PublishingTimeToolsService : ITimeToolsService
    {
        public TimeToolsSnapshot Current { get; private set; } = TimeToolsSnapshot.Default;
        public TimeSpan? LastStartedDuration { get; private set; }
        public event EventHandler<TimeToolsSnapshot>? SnapshotChanged;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public bool StartTimer(TimeSpan duration) { LastStartedDuration = duration; return true; }
        public bool PauseTimer() => false;
        public bool ResumeTimer() => false;
        public bool CancelTimer() => false;
        public bool ConsumePendingCompletion() => false;
        public bool StartStopwatch() => false;
        public bool PauseStopwatch() => false;
        public bool AddLap() => false;
        public bool ResetStopwatch() => false;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Publish(TimeToolsSnapshot snapshot)
        {
            Current = snapshot;
            SnapshotChanged?.Invoke(this, snapshot);
        }
    }

    [TestMethod]
    public async Task ExpiredPersistedTimer_RaisesCompletionOnlyOnce()
    {
        var now = DateTimeOffset.Parse("2026-07-22T10:00:00Z");
        var persisted = new TimerPersistentState(
            TimerPersistentState.CurrentSchemaVersion,
            TimerRunState.Running,
            TimeSpan.FromMinutes(1).Ticks,
            TimeSpan.FromSeconds(20).Ticks,
            now.AddSeconds(-1),
            false);
        await using var service = new TimeToolsService(
            new MemoryStore(persisted),
            timeProvider: new MutableTimeProvider(now));

        await service.InitializeAsync();

        Assert.AreEqual(TimerRunState.Completed, service.Current.TimerState);
        Assert.IsTrue(service.ConsumePendingCompletion());
        Assert.IsFalse(service.ConsumePendingCompletion());
    }

    [TestMethod]
    public async Task PendingCompletion_SurvivesRestartUntilConsumed()
    {
        var persisted = new TimerPersistentState(
            TimerPersistentState.CurrentSchemaVersion,
            TimerRunState.Completed,
            TimeSpan.FromMinutes(5).Ticks,
            0,
            null,
            true);
        await using var service = new TimeToolsService(new MemoryStore(persisted));

        await service.InitializeAsync();

        Assert.AreEqual(TimerRunState.Completed, service.Current.TimerState);
        Assert.IsTrue(service.ConsumePendingCompletion());
    }

    [TestMethod]
    public async Task RunningTimer_RestoresFromUtcTargetWithoutResettingDuration()
    {
        var now = DateTimeOffset.Parse("2026-07-22T10:00:00Z");
        var persisted = new TimerPersistentState(
            TimerPersistentState.CurrentSchemaVersion,
            TimerRunState.Running,
            TimeSpan.FromMinutes(5).Ticks,
            TimeSpan.FromMinutes(4).Ticks,
            now.AddMinutes(3),
            false);
        await using var service = new TimeToolsService(
            new MemoryStore(persisted),
            timeProvider: new MutableTimeProvider(now));

        await service.InitializeAsync();

        Assert.AreEqual(TimerRunState.Running, service.Current.TimerState);
        Assert.AreEqual(TimeSpan.FromMinutes(5), service.Current.TimerDuration);
        Assert.AreEqual(TimeSpan.FromMinutes(3), service.Current.TimerRemaining);
    }

    [TestMethod]
    public async Task PausedTimer_RestoresExactRemainingTime()
    {
        var persisted = new TimerPersistentState(
            TimerPersistentState.CurrentSchemaVersion,
            TimerRunState.Paused,
            TimeSpan.FromMinutes(5).Ticks,
            TimeSpan.FromMinutes(2.5).Ticks,
            null,
            false);
        await using var service = new TimeToolsService(new MemoryStore(persisted));

        await service.InitializeAsync();

        Assert.AreEqual(TimerRunState.Paused, service.Current.TimerState);
        Assert.AreEqual(TimeSpan.FromMinutes(2.5), service.Current.TimerRemaining);
        Assert.IsNull(service.Current.TimerTargetUtc);
    }

    [TestMethod]
    public async Task ExpiredTimer_PlaysAlarmWhenRestored()
    {
        var now = DateTimeOffset.Parse("2026-07-22T10:00:00Z");
        var persisted = new TimerPersistentState(
            TimerPersistentState.CurrentSchemaVersion,
            TimerRunState.Running,
            TimeSpan.FromMinutes(1).Ticks,
            TimeSpan.FromSeconds(10).Ticks,
            now.AddSeconds(-1),
            false);
        var alarm = new RecordingAlarmPlayer();
        await using var service = new TimeToolsService(
            new MemoryStore(persisted),
            timeProvider: new MutableTimeProvider(now),
            alarmPlayer: alarm);

        await service.InitializeAsync();

        Assert.AreEqual(1, alarm.PlayCount);
    }

    [TestMethod]
    public async Task CancellingCompletedTimer_StopsActiveAlarm()
    {
        var persisted = new TimerPersistentState(
            TimerPersistentState.CurrentSchemaVersion,
            TimerRunState.Completed,
            TimeSpan.FromMinutes(1).Ticks,
            0,
            null,
            true);
        var alarm = new RecordingAlarmPlayer();
        await using var service = new TimeToolsService(
            new MemoryStore(persisted),
            alarmPlayer: alarm);

        await service.InitializeAsync();
        Assert.AreEqual(1, alarm.PlayCount);

        Assert.IsTrue(service.CancelTimer());

        Assert.AreEqual(1, alarm.StopCount);
        Assert.AreEqual(TimerRunState.Idle, service.Current.TimerState);
    }

    [TestMethod]
    public async Task StopwatchLap_UsesMonotonicTime()
    {
        var time = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-22T10:00:00Z"));
        await using var service = new TimeToolsService(new MemoryStore(), timeProvider: time);
        await service.InitializeAsync();
        Assert.IsTrue(service.StartStopwatch());

        time.Advance(TimeSpan.FromSeconds(3.25));
        Assert.IsTrue(service.AddLap());

        Assert.AreEqual(TimeSpan.FromSeconds(3.25), service.Current.Laps.Single());
    }

    [TestMethod]
    public async Task StartingStopwatch_DoesNotStartCountdownTimer()
    {
        await using var service = new TimeToolsService(new MemoryStore());
        await service.InitializeAsync();

        Assert.IsTrue(service.StartStopwatch());

        Assert.IsTrue(service.Current.IsStopwatchRunning);
        Assert.AreEqual(TimerRunState.Idle, service.Current.TimerState);
        Assert.AreEqual(TimeSpan.Zero, service.Current.TimerRemaining);
    }

    [TestMethod]
    public async Task CompactStopwatchCommand_PausesStopwatchWithoutTouchingTimer()
    {
        await using var service = new TimeToolsService(new MemoryStore());
        await service.InitializeAsync();
        using var viewModel = new TimeToolsViewModel(service);
        Assert.IsTrue(service.StartStopwatch());

        Assert.AreEqual("Kronometre çalışıyor", viewModel.CompactStatusText);
        Assert.IsTrue(viewModel.CompactPrimaryCommand.CanExecute(null));
        viewModel.CompactPrimaryCommand.Execute(null);

        Assert.IsFalse(service.Current.IsStopwatchRunning);
        Assert.AreEqual(TimerRunState.Idle, service.Current.TimerState);
        Assert.AreEqual("Kronometre duraklatıldı", viewModel.CompactStatusText);
    }

    [TestMethod]
    public async Task TimerAndStopwatch_KeepIndependentRunStates()
    {
        var time = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-22T10:00:00Z"));
        await using var service = new TimeToolsService(new MemoryStore(), timeProvider: time);
        await service.InitializeAsync();

        Assert.IsTrue(service.StartStopwatch());
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.IsTrue(service.StartTimer(TimeSpan.FromMinutes(1)));
        Assert.IsTrue(service.Current.IsStopwatchRunning);
        Assert.AreEqual(TimerRunState.Running, service.Current.TimerState);

        Assert.IsTrue(service.PauseTimer());
        Assert.IsTrue(service.Current.IsStopwatchRunning);
        Assert.IsTrue(service.PauseStopwatch());
        Assert.AreEqual(TimerRunState.Paused, service.Current.TimerState);
    }

    [TestMethod]
    public async Task SystemResume_ReconcilesExpiredTimerAndRunningStopwatch()
    {
        var time = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-22T10:00:00Z"));
        var resume = new FakeResumeService();
        var alarm = new RecordingAlarmPlayer();
        await using var service = new TimeToolsService(
            new MemoryStore(),
            resume,
            time,
            alarm);
        await service.InitializeAsync();
        Assert.IsTrue(service.StartTimer(TimeSpan.FromSeconds(10)));
        Assert.IsTrue(service.StartStopwatch());

        time.Advance(TimeSpan.FromSeconds(12));
        resume.RaiseResumed();

        Assert.AreEqual(TimerRunState.Completed, service.Current.TimerState);
        Assert.AreEqual(TimeSpan.Zero, service.Current.TimerRemaining);
        Assert.AreEqual(TimeSpan.FromSeconds(12), service.Current.StopwatchElapsed);
        Assert.AreEqual(1, alarm.PlayCount);
    }

    [TestMethod]
    public async Task SystemResume_DoesNotConsumePausedTimerTime()
    {
        var time = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-22T10:00:00Z"));
        var resume = new FakeResumeService();
        await using var service = new TimeToolsService(new MemoryStore(), resume, time);
        await service.InitializeAsync();
        Assert.IsTrue(service.StartTimer(TimeSpan.FromSeconds(10)));
        time.Advance(TimeSpan.FromSeconds(4));
        Assert.IsTrue(service.PauseTimer());

        time.Advance(TimeSpan.FromHours(1));
        resume.RaiseResumed();

        Assert.AreEqual(TimerRunState.Paused, service.Current.TimerState);
        Assert.AreEqual(TimeSpan.FromSeconds(6), service.Current.TimerRemaining);
    }

    [TestMethod]
    public async Task StopwatchLaps_KeepLatestOneHundredInChronologicalOrder()
    {
        var time = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-22T10:00:00Z"));
        await using var service = new TimeToolsService(new MemoryStore(), timeProvider: time);
        await service.InitializeAsync();
        Assert.IsTrue(service.StartStopwatch());

        for (var index = 1; index <= 105; index++)
        {
            time.Advance(TimeSpan.FromSeconds(1));
            Assert.IsTrue(service.AddLap());
        }

        Assert.HasCount(100, service.Current.Laps);
        Assert.AreEqual(TimeSpan.FromSeconds(6), service.Current.Laps[0]);
        Assert.AreEqual(TimeSpan.FromSeconds(105), service.Current.Laps[^1]);
    }

    [TestMethod]
    public async Task HoverSilenceCommand_StopsAlarmAndDismissesCompletedTimer()
    {
        var persisted = new TimerPersistentState(
            TimerPersistentState.CurrentSchemaVersion,
            TimerRunState.Completed,
            TimeSpan.FromMinutes(1).Ticks,
            0,
            null,
            true);
        var alarm = new RecordingAlarmPlayer();
        await using var service = new TimeToolsService(new MemoryStore(persisted), alarmPlayer: alarm);
        await service.InitializeAsync();
        using var viewModel = new TimeToolsViewModel(service);

        viewModel.CompactSecondaryCommand.Execute(null);

        Assert.AreEqual(TimerRunState.Idle, service.Current.TimerState);
        Assert.AreEqual(1, alarm.StopCount);
    }

    [TestMethod]
    public async Task RapidStateChanges_PersistSeriallyAndFinishWithLatestState()
    {
        var store = new BlockingStore();
        var service = new TimeToolsService(store);
        await service.InitializeAsync();
        store.BlockNextSave();

        Assert.IsTrue(service.StartTimer(TimeSpan.FromMinutes(5)));
        await store.WaitUntilBlockedAsync();
        Assert.IsTrue(service.PauseTimer());

        Assert.AreEqual(1, store.MaximumConcurrency);
        store.ReleaseBlockedSave();
        await service.DisposeAsync();

        Assert.AreEqual(1, store.MaximumConcurrency);
        Assert.AreEqual(TimerRunState.Paused, store.Snapshots[^1].State);
        Assert.IsTrue(store.Snapshots[^1].RemainingTicks > 0);
    }

    [TestMethod]
    public async Task PersistenceFailure_IsContainedAndRateLimitedInTechnicalLog()
    {
        var log = new RecordingLogService();
        var service = new TimeToolsService(new FailingStore(), log: log);

        await service.InitializeAsync();
        Assert.IsTrue(service.StartTimer(TimeSpan.FromMinutes(1)));
        await service.DisposeAsync();

        Assert.AreEqual(
            1,
            log.EventIds.Count(eventId => eventId == TechnicalEventIds.TimeStatePersistFailed));
    }

    private sealed class MemoryStore(TimerPersistentState? state = null) : ITimerStateStore
    {
        private TimerPersistentState? _state = state;
        public Task<TimerPersistentState?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_state);
        public Task SaveAsync(TimerPersistentState state, CancellationToken cancellationToken = default)
        {
            _state = state;
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingStore : ITimerStateStore
    {
        private readonly object _gate = new();
        private TaskCompletionSource _blocked = NewCompletion();
        private TaskCompletionSource _release = NewCompletion();
        private bool _blockNext;
        private int _concurrency;

        public List<TimerPersistentState> Snapshots { get; } = [];

        public int MaximumConcurrency { get; private set; }

        public Task<TimerPersistentState?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<TimerPersistentState?>(null);

        public async Task SaveAsync(
            TimerPersistentState state,
            CancellationToken cancellationToken = default)
        {
            var concurrency = Interlocked.Increment(ref _concurrency);
            MaximumConcurrency = Math.Max(MaximumConcurrency, concurrency);
            try
            {
                bool block;
                lock (_gate)
                {
                    Snapshots.Add(state);
                    block = _blockNext;
                    _blockNext = false;
                }

                if (block)
                {
                    _blocked.TrySetResult();
                    await _release.Task.WaitAsync(cancellationToken);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _concurrency);
            }
        }

        public void BlockNextSave()
        {
            lock (_gate)
            {
                _blocked = NewCompletion();
                _release = NewCompletion();
                _blockNext = true;
            }
        }

        public Task WaitUntilBlockedAsync() =>
            _blocked.Task.WaitAsync(TimeSpan.FromSeconds(5));

        public void ReleaseBlockedSave() => _release.TrySetResult();

        private static TaskCompletionSource NewCompletion() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class FailingStore : ITimerStateStore
    {
        public Task<TimerPersistentState?> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<TimerPersistentState?>(null);

        public Task SaveAsync(
            TimerPersistentState state,
            CancellationToken cancellationToken = default) =>
            Task.FromException(new IOException("persist failed"));
    }

    private sealed class RecordingLogService : ILogService
    {
        public List<string> EventIds { get; } = [];

        public string LogDirectoryPath => string.Empty;

        public Exception? LastFailure => null;

        public long DroppedEntryCount => 0;

        public void Write(
            TechnicalLogLevel level,
            string eventId,
            string category,
            string message,
            Exception? exception = null,
            IReadOnlyDictionary<string, object?>? properties = null) =>
            EventIds.Add(eventId);

        public ValueTask FlushAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RecordingAlarmPlayer : ITimerAlarmPlayer
    {
        public int PlayCount { get; private set; }
        public int StopCount { get; private set; }
        public void Play() => PlayCount++;
        public void Stop() => StopCount++;
    }

    private sealed class FakeResumeService : ISystemResumeService
    {
        public event EventHandler? Resumed;
        public void Start() { }
        public void RaiseResumed() => Resumed?.Invoke(this, EventArgs.Empty);
        public void Dispose() { }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;
        private long _timestamp;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public override long GetTimestamp() => _timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void Advance(TimeSpan duration)
        {
            _utcNow += duration;
            _timestamp += duration.Ticks;
        }
    }
}
