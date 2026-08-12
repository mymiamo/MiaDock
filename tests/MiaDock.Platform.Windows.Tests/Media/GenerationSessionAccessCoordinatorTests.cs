using MiaDock.Platform.Windows.Media;

namespace MiaDock.Platform.Windows.Tests.Media;

[TestClass]
public sealed class GenerationSessionAccessCoordinatorTests
{
    [TestMethod]
    public async Task Switch_CancelsOldReadAndPreventsOldResultPublication()
    {
        await using var coordinator = new GenerationSessionAccessCoordinator<FakeSession>();
        var sessionA = new FakeSession("A");
        var leaseA = coordinator.Switch(sessionA)!;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldRead = coordinator.ExecuteAsync(
            leaseA,
            async (session, token) =>
            {
                session.CallCount++;
                started.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return session.Id;
            },
            CancellationToken.None);
        await started.Task;

        var sessionB = new FakeSession("B");
        var leaseB = coordinator.Switch(sessionB)!;
        var resultB = await coordinator.ExecuteAsync(
            leaseB,
            (session, _) => Task.FromResult(session.Id),
            CancellationToken.None);

        Assert.AreEqual("B", resultB);
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await oldRead);
        Assert.IsFalse(coordinator.IsCurrent(leaseA));
        Assert.IsTrue(coordinator.IsCurrent(leaseB));
    }

    [TestMethod]
    public async Task NewSession_IsNotBlockedByOldSessionThatIgnoresCancellation()
    {
        await using var coordinator = new GenerationSessionAccessCoordinator<FakeSession>();
        var oldGate = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var leaseA = coordinator.Switch(new FakeSession("A"))!;
        var oldRead = coordinator.ExecuteAsync(
            leaseA,
            (_, _) => oldGate.Task,
            CancellationToken.None);
        await Task.Yield();

        var leaseB = coordinator.Switch(new FakeSession("B"))!;
        var newRead = coordinator.ExecuteAsync(
            leaseB,
            (session, _) => Task.FromResult(session.Id),
            CancellationToken.None);

        Assert.AreEqual("B", await newRead.WaitAsync(TimeSpan.FromSeconds(1)));
        oldGate.SetResult("A");
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await oldRead);
    }

    [TestMethod]
    public async Task SameSession_ReadsAreStrictlySerialized()
    {
        await using var coordinator = new GenerationSessionAccessCoordinator<FakeSession>();
        var session = new FakeSession("A");
        var lease = coordinator.Switch(session)!;

        var reads = Enumerable.Range(0, 100).Select(_ => coordinator.ExecuteAsync(
            lease,
            async (current, token) =>
            {
                var active = Interlocked.Increment(ref current.ActiveCalls);
                UpdateMaximum(ref current.MaximumConcurrentCalls, active);
                try { await Task.Delay(1, token); return current.Id; }
                finally { Interlocked.Decrement(ref current.ActiveCalls); }
            },
            CancellationToken.None));

        await Task.WhenAll(reads);
        Assert.AreEqual(1, session.MaximumConcurrentCalls);
    }

    [TestMethod]
    public async Task Stress_OneThousandRapidSwitchesRejectAllStaleLeases()
    {
        await using var coordinator = new GenerationSessionAccessCoordinator<FakeSession>();
        GenerationSessionAccessCoordinator<FakeSession>.SessionLease? previous = null;
        var staleCalls = 0;

        for (var index = 0; index < 1000; index++)
        {
            var current = coordinator.Switch(new FakeSession(index.ToString()))!;
            if (previous is not null)
            {
                try
                {
                    await coordinator.ExecuteAsync(
                        previous,
                        (session, _) =>
                        {
                            Interlocked.Increment(ref staleCalls);
                            return Task.FromResult(session.Id);
                        },
                        CancellationToken.None);
                    Assert.Fail("A retired session accepted a new native call.");
                }
                catch (OperationCanceledException)
                {
                }
            }
            previous = current;
        }

        Assert.AreEqual(0, staleCalls);
    }

    [TestMethod]
    public async Task Dispose_PreventsLateCallbacksFromStartingNewCalls()
    {
        var coordinator = new GenerationSessionAccessCoordinator<FakeSession>();
        var lease = coordinator.Switch(new FakeSession("A"))!;
        await coordinator.DisposeAsync();
        var calls = 0;

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await coordinator.ExecuteAsync(
                lease,
                (session, _) =>
                {
                    calls++;
                    return Task.FromResult(session.Id);
                },
                CancellationToken.None));
        Assert.AreEqual(0, calls);
    }

    [TestMethod]
    public async Task Dispose_WaitsForInFlightNativeCallToDrain()
    {
        var coordinator = new GenerationSessionAccessCoordinator<FakeSession>();
        var lease = coordinator.Switch(new FakeSession("A"))!;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var inFlight = coordinator.ExecuteAsync(
            lease,
            async (session, _) =>
            {
                started.SetResult();
                // Ignore cancellation so Dispose must wait for the native call to finish.
                return await release.Task;
            },
            CancellationToken.None);
        await started.Task;

        var disposeTask = coordinator.DisposeAsync().AsTask();
        Assert.IsFalse(
            await Task.WhenAny(disposeTask, Task.Delay(50)) == disposeTask,
            "Dispose returned before the in-flight native call drained.");

        release.SetResult(lease.Session.Id);
        await disposeTask.WaitAsync(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await inFlight);
    }

    private static void UpdateMaximum(ref int target, int value)
    {
        while (true)
        {
            var current = Volatile.Read(ref target);
            if (current >= value || Interlocked.CompareExchange(ref target, value, current) == current) return;
        }
    }

    private sealed class FakeSession(string id)
    {
        public string Id { get; } = id;
        public int CallCount;
        public int ActiveCalls;
        public int MaximumConcurrentCalls;
    }
}
