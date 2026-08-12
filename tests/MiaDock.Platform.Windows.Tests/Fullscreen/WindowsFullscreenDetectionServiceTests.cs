using MiaDock.Core.Threading;
using MiaDock.Core.Logging;
using MiaDock.Platform.Windows.Fullscreen;

namespace MiaDock.Platform.Windows.Tests.Fullscreen;

[TestClass]
public sealed class WindowsFullscreenDetectionServiceTests
{
    [TestMethod]
    public void RecoveryPoll_ClearsStaleFullscreenSnapshotWithoutAnotherWindowEvent()
    {
        var snapshots = new Queue<FullscreenSnapshot>(
        [
            new(true, 101, 201, FullscreenDetectionReason.ExclusiveDirect3D),
            FullscreenSnapshot.None
        ]);
        var timeProvider = new ManualTimeProvider();
        using var service = new WindowsFullscreenDetectionService(
            new ImmediateDispatcher(),
            snapshots.Dequeue,
            timeProvider,
            TimeSpan.FromMilliseconds(500));
        var states = new List<bool>();
        service.StateChanged += (_, snapshot) => states.Add(snapshot.IsFullscreen);

        service.Refresh();

        Assert.IsTrue(service.Current.IsFullscreen);
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), timeProvider.RecoveryTimer.DueTime);

        timeProvider.RecoveryTimer.Fire();

        Assert.IsFalse(service.Current.IsFullscreen);
        CollectionAssert.AreEqual(new[] { true, false }, states);
        Assert.AreEqual(Timeout.InfiniteTimeSpan, timeProvider.RecoveryTimer.DueTime);
    }

    [TestMethod]
    [TestCategory("FullscreenSoak")]
    public void RecoveryPoll_TwoVirtualHoursDoNotWakeUiForUnchangedFullscreen()
    {
        var snapshot = new FullscreenSnapshot(
            true,
            101,
            201,
            FullscreenDetectionReason.WindowCoversMonitor);
        var evaluationCount = 0;
        var dispatcher = new RecordingDispatcher { HasThreadAccess = true };
        var timeProvider = new ManualTimeProvider();
        using var service = new WindowsFullscreenDetectionService(
            dispatcher,
            () =>
            {
                evaluationCount++;
                return snapshot;
            },
            timeProvider,
            TimeSpan.FromMilliseconds(500));
        var stateChangeCount = 0;
        service.StateChanged += (_, _) => stateChangeCount++;
        service.Refresh();
        dispatcher.HasThreadAccess = false;

        const int twoHoursAtTwoSamplesPerSecond = 2 * 60 * 60 * 2;
        for (var sample = 0; sample < twoHoursAtTwoSamplesPerSecond; sample++)
        {
            timeProvider.RecoveryTimer.Fire();
        }

        Assert.AreEqual(twoHoursAtTwoSamplesPerSecond + 1, evaluationCount);
        Assert.AreEqual(1, stateChangeCount);
        Assert.AreEqual(0, dispatcher.EnqueueCount);
        Assert.AreEqual(2, timeProvider.TimerCount);
    }

    [TestMethod]
    [TestCategory("FullscreenSoak")]
    public void ThousandEntryExitCycles_ReuseTimersAndPublishEveryRealTransitionOnce()
    {
        var next = FullscreenSnapshot.None;
        var timeProvider = new ManualTimeProvider();
        using var service = new WindowsFullscreenDetectionService(
            new ImmediateDispatcher(),
            () => next,
            timeProvider,
            TimeSpan.FromMilliseconds(500));
        var transitions = 0;
        service.StateChanged += (_, _) => transitions++;

        for (var cycle = 0; cycle < 1_000; cycle++)
        {
            next = new FullscreenSnapshot(
                true,
                100 + cycle,
                200,
                FullscreenDetectionReason.WindowCoversMonitor);
            service.Refresh();
            next = FullscreenSnapshot.None;
            service.Refresh();
        }

        Assert.AreEqual(2_000, transitions);
        Assert.AreEqual(2, timeProvider.TimerCount);
        Assert.AreEqual(Timeout.InfiniteTimeSpan, timeProvider.RecoveryTimer.DueTime);
    }

    [TestMethod]
    public void BackgroundTransition_IsPublishedOnlyThroughUiDispatcher()
    {
        var snapshots = new Queue<FullscreenSnapshot>(
        [
            new(true, 101, 201, FullscreenDetectionReason.WindowCoversMonitor),
            FullscreenSnapshot.None
        ]);
        var dispatcher = new RecordingDispatcher { HasThreadAccess = true };
        var timeProvider = new ManualTimeProvider();
        using var service = new WindowsFullscreenDetectionService(
            dispatcher,
            snapshots.Dequeue,
            timeProvider,
            TimeSpan.FromMilliseconds(500));
        service.Refresh();
        dispatcher.HasThreadAccess = false;

        timeProvider.RecoveryTimer.Fire();

        Assert.IsTrue(service.Current.IsFullscreen);
        Assert.AreEqual(1, dispatcher.EnqueueCount);
        dispatcher.Drain();
        Assert.IsFalse(service.Current.IsFullscreen);
    }

    [TestMethod]
    public void DetectionFailures_AreRateLimitedAndRecoveryRemainsScheduled()
    {
        var calls = 0;
        var timeProvider = new ManualTimeProvider();
        var log = new RecordingLogService();
        using var service = new WindowsFullscreenDetectionService(
            new ImmediateDispatcher(),
            () => calls++ == 0
                ? new FullscreenSnapshot(true, 101, 201, FullscreenDetectionReason.WindowCoversMonitor)
                : throw new InvalidOperationException("transient"),
            timeProvider,
            TimeSpan.FromMilliseconds(500),
            log);
        service.Refresh();

        timeProvider.RecoveryTimer.Fire();
        timeProvider.RecoveryTimer.Fire();

        Assert.IsInstanceOfType<InvalidOperationException>(service.LastFailure);
        Assert.AreEqual(1, log.EventIds.Count(id => id == TechnicalEventIds.FullscreenDetectionFailed));
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), timeProvider.RecoveryTimer.DueTime);
    }

    [TestMethod]
    public void Dispose_StopsEventAndRecoveryTimers()
    {
        var timeProvider = new ManualTimeProvider();
        var service = new WindowsFullscreenDetectionService(
            new ImmediateDispatcher(),
            () => new FullscreenSnapshot(true, 101, 201, FullscreenDetectionReason.WindowCoversMonitor),
            timeProvider,
            TimeSpan.FromMilliseconds(500));
        service.Refresh();

        service.Dispose();

        Assert.IsTrue(timeProvider.EventTimer.IsDisposed);
        Assert.IsTrue(timeProvider.RecoveryTimer.IsDisposed);
    }

    [TestMethod]
    public void DispatcherRejection_SchedulesRetryAndDoesNotLeaveFullscreenStuck()
    {
        var snapshots = new Queue<FullscreenSnapshot>(
        [
            new(true, 101, 201, FullscreenDetectionReason.WindowCoversMonitor),
            FullscreenSnapshot.None,
            FullscreenSnapshot.None
        ]);
        var dispatcher = new RecordingDispatcher { HasThreadAccess = true };
        var timeProvider = new ManualTimeProvider();
        using var service = new WindowsFullscreenDetectionService(
            dispatcher,
            snapshots.Dequeue,
            timeProvider,
            TimeSpan.FromMilliseconds(500));
        service.Refresh();
        dispatcher.HasThreadAccess = false;
        dispatcher.AcceptEnqueue = false;

        timeProvider.RecoveryTimer.Fire();

        Assert.IsTrue(service.Current.IsFullscreen);
        Assert.AreEqual(TimeSpan.FromMilliseconds(100), timeProvider.EventTimer.DueTime);
        dispatcher.AcceptEnqueue = true;
        timeProvider.EventTimer.Fire();
        dispatcher.Drain();
        Assert.IsFalse(service.Current.IsFullscreen);
    }

    [TestMethod]
    public async Task ConcurrentRecoveryCallbacks_AreSerializedAndCoalesced()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var evaluationCount = 0;
        var concurrency = 0;
        var maximumConcurrency = 0;
        var timeProvider = new ManualTimeProvider();
        using var service = new WindowsFullscreenDetectionService(
            new ImmediateDispatcher(),
            () =>
            {
                var currentConcurrency = Interlocked.Increment(ref concurrency);
                maximumConcurrency = Math.Max(maximumConcurrency, currentConcurrency);
                var call = Interlocked.Increment(ref evaluationCount);
                try
                {
                    if (call == 2)
                    {
                        entered.Set();
                        release.Wait(TimeSpan.FromSeconds(5));
                    }

                    return new FullscreenSnapshot(
                        true,
                        101,
                        201,
                        FullscreenDetectionReason.WindowCoversMonitor);
                }
                finally
                {
                    Interlocked.Decrement(ref concurrency);
                }
            },
            timeProvider,
            TimeSpan.FromMilliseconds(500));
        service.Refresh();

        var first = Task.Run(timeProvider.RecoveryTimer.Fire);
        Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
        var second = Task.Run(timeProvider.RecoveryTimer.Fire);
        await second;
        release.Set();
        await first;

        Assert.AreEqual(1, maximumConcurrency);
        Assert.AreEqual(3, evaluationCount);
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public bool HasThreadAccess => true;

        public bool TryEnqueue(Action callback)
        {
            callback();
            return true;
        }
    }

    private sealed class RecordingDispatcher : IUiDispatcher
    {
        private readonly Queue<Action> _callbacks = new();

        public bool HasThreadAccess { get; set; }

        public bool AcceptEnqueue { get; set; } = true;

        public int EnqueueCount { get; private set; }

        public bool TryEnqueue(Action callback)
        {
            EnqueueCount++;
            if (!AcceptEnqueue)
            {
                return false;
            }

            _callbacks.Enqueue(callback);
            return true;
        }

        public void Drain()
        {
            HasThreadAccess = true;
            while (_callbacks.TryDequeue(out var callback))
            {
                callback();
            }
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];

        public ManualTimer EventTimer => _timers[0];

        public ManualTimer RecoveryTimer => _timers[1];

        public int TimerCount => _timers.Count;

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, dueTime, period);
            _timers.Add(timer);
            return timer;
        }
    }

    private sealed class ManualTimer(
        TimerCallback callback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period) : ITimer
    {
        private bool _disposed;

        public bool IsDisposed => _disposed;

        public TimeSpan DueTime { get; private set; } = dueTime;

        public TimeSpan Period { get; private set; } = period;

        public bool Change(TimeSpan nextDueTime, TimeSpan nextPeriod)
        {
            if (_disposed)
            {
                return false;
            }

            DueTime = nextDueTime;
            Period = nextPeriod;
            return true;
        }

        public void Fire()
        {
            if (!_disposed && DueTime != Timeout.InfiniteTimeSpan)
            {
                callback(state);
            }
        }

        public void Dispose() => _disposed = true;

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
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
}
