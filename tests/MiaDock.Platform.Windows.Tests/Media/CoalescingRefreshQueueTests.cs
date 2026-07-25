using MiaDock.Platform.Windows.Media;

namespace MiaDock.Platform.Windows.Tests.Media;

[TestClass]
public sealed class CoalescingRefreshQueueTests
{
    [TestMethod]
    public async Task MinimumInterval_ThrottlesContinuousFollowUpRefreshes()
    {
        var starts = new List<long>();
        CoalescingRefreshQueue? queue = null;
        queue = new CoalescingRefreshQueue(
            _ =>
            {
                starts.Add(System.Diagnostics.Stopwatch.GetTimestamp());
                if (starts.Count == 1)
                {
                    queue!.Request();
                }

                return Task.CompletedTask;
            },
            TimeSpan.FromMilliseconds(60));

        await using (queue)
        {
            queue.Request();
            await queue.WaitForIdleAsync();
        }

        Assert.HasCount(2, starts);
        Assert.IsGreaterThanOrEqualTo(
            TimeSpan.FromMilliseconds(45),
            System.Diagnostics.Stopwatch.GetElapsedTime(starts[0], starts[1]));
    }

    [TestMethod]
    public async Task RequestsWhileRunning_AreCoalescedIntoOneFollowUpRefresh()
    {
        var firstRefreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;
        await using var queue = new CoalescingRefreshQueue(async cancellationToken =>
        {
            var invocation = Interlocked.Increment(ref invocationCount);
            if (invocation == 1)
            {
                firstRefreshStarted.SetResult();
                await releaseFirstRefresh.Task.WaitAsync(cancellationToken);
            }
        });

        queue.Request();
        await firstRefreshStarted.Task;
        queue.Request();
        queue.Request();
        queue.Request();
        releaseFirstRefresh.SetResult();
        await queue.WaitForIdleAsync();

        Assert.AreEqual(2, invocationCount);
        Assert.IsNull(queue.LastFailure);
    }

    [TestMethod]
    public async Task FiftyThousandRapidRequests_DoNotCreateUnboundedWork()
    {
        var firstRefreshStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstRefresh = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;
        await using var queue = new CoalescingRefreshQueue(async cancellationToken =>
        {
            if (Interlocked.Increment(ref invocationCount) == 1)
            {
                firstRefreshStarted.TrySetResult();
                await releaseFirstRefresh.Task.WaitAsync(cancellationToken);
            }
        });

        queue.Request();
        await firstRefreshStarted.Task;
        Parallel.For(0, 50_000, _ => queue.Request());
        releaseFirstRefresh.TrySetResult();
        await queue.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.AreEqual(2, invocationCount);
        Assert.IsNull(queue.LastFailure);
    }
}
