using MiaDock.Core.Threading;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class UiThreadEventSubscriptionTests
{
    [TestMethod]
    public async Task DisposeAsync_OnUiThread_UnsubscribesImmediatelyAndOnlyOnce()
    {
        var dispatcher = new TestDispatcher(hasThreadAccess: true);
        var unsubscribeCount = 0;
        var subscription = new UiThreadEventSubscription(
            dispatcher,
            () => unsubscribeCount++);

        await subscription.DisposeAsync();
        await subscription.DisposeAsync();

        Assert.AreEqual(1, unsubscribeCount);
        Assert.AreEqual(0, dispatcher.PendingCount);
    }

    [TestMethod]
    public async Task DisposeAsync_OffUiThread_MarshalsUnsubscribeToDispatcher()
    {
        var dispatcher = new TestDispatcher(hasThreadAccess: false);
        var unsubscribeCount = 0;
        var subscription = new UiThreadEventSubscription(
            dispatcher,
            () => unsubscribeCount++);

        var disposal = subscription.DisposeAsync().AsTask();

        Assert.AreEqual(0, unsubscribeCount);
        Assert.IsFalse(disposal.IsCompleted);
        dispatcher.RunPending();
        await disposal;
        Assert.AreEqual(1, unsubscribeCount);
    }

    [TestMethod]
    public async Task DisposeAsync_WhenUiQueueIsUnavailable_DoesNotInvokeOnWrongThread()
    {
        var dispatcher = new TestDispatcher(hasThreadAccess: false, acceptsCallbacks: false);
        var unsubscribeCount = 0;
        var subscription = new UiThreadEventSubscription(
            dispatcher,
            () => unsubscribeCount++);

        await subscription.DisposeAsync();

        Assert.AreEqual(0, unsubscribeCount);
    }

    [TestMethod]
    public async Task DisposeAsync_WhenUnsubscribeThrows_CompletesShutdown()
    {
        var dispatcher = new TestDispatcher(hasThreadAccess: true);
        var subscription = new UiThreadEventSubscription(
            dispatcher,
            () => throw new InvalidOperationException("Native event source is already closed."));

        await subscription.DisposeAsync();
    }

    private sealed class TestDispatcher(
        bool hasThreadAccess,
        bool acceptsCallbacks = true) : IUiDispatcher
    {
        private readonly Queue<Action> _pending = new();

        public bool HasThreadAccess { get; } = hasThreadAccess;
        public int PendingCount => _pending.Count;

        public bool TryEnqueue(Action callback)
        {
            if (!acceptsCallbacks)
            {
                return false;
            }

            _pending.Enqueue(callback);
            return true;
        }

        public void RunPending()
        {
            while (_pending.TryDequeue(out var callback))
            {
                callback();
            }
        }
    }
}
