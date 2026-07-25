using MiaDock.Platform.Windows.Threading;

namespace MiaDock.Platform.Windows.Tests.Threading;

[TestClass]
public sealed class CoalescingActionSchedulerTests
{
    [TestMethod]
    public void FiftyThousandRequests_QueueOnlyOneAction()
    {
        var queued = new Queue<Action>();
        var executions = 0;
        var scheduler = new CoalescingActionScheduler(
            action =>
            {
                queued.Enqueue(action);
                return true;
            },
            () => executions++);

        Parallel.For(0, 50_000, _ => scheduler.Request());

        Assert.HasCount(1, queued);
        queued.Dequeue()();
        Assert.AreEqual(1, executions);
        Assert.HasCount(0, queued);
    }

    [TestMethod]
    public void RequestDuringExecution_QueuesOneFollowUp()
    {
        var queued = new Queue<Action>();
        CoalescingActionScheduler? scheduler = null;
        var executions = 0;
        scheduler = new CoalescingActionScheduler(
            action =>
            {
                queued.Enqueue(action);
                return true;
            },
            () =>
            {
                executions++;
                if (executions == 1)
                {
                    scheduler!.Request();
                    scheduler.Request();
                }
            });

        scheduler.Request();
        queued.Dequeue()();

        Assert.HasCount(1, queued);
        queued.Dequeue()();
        Assert.AreEqual(2, executions);
    }

    [TestMethod]
    public void FailedSchedule_CanBeRetried()
    {
        var attempts = 0;
        var executions = 0;
        var queued = new Queue<Action>();
        var scheduler = new CoalescingActionScheduler(
            action =>
            {
                attempts++;
                if (attempts == 1)
                {
                    return false;
                }

                queued.Enqueue(action);
                return true;
            },
            () => executions++);

        Assert.IsFalse(scheduler.Request());
        Assert.IsTrue(scheduler.Request());
        queued.Dequeue()();

        Assert.AreEqual(1, executions);
    }
}
