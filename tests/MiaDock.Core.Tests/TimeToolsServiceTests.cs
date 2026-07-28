using MiaDock.Modules.Time.Models;
using MiaDock.Modules.Time.Persistence;
using MiaDock.Modules.Time.Services;
using MiaDock.Modules.Time.ViewModels;
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
        public event EventHandler<TimeToolsSnapshot>? SnapshotChanged;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public bool StartTimer(TimeSpan duration) => false;
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

    private sealed class RecordingAlarmPlayer : ITimerAlarmPlayer
    {
        public int PlayCount { get; private set; }
        public int StopCount { get; private set; }
        public void Play() => PlayCount++;
        public void Stop() => StopCount++;
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
