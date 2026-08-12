using MiaDock.Platform.Windows.Fullscreen;

namespace MiaDock.Platform.Windows.Tests.Fullscreen;

[TestClass]
public sealed class ExclusiveFullscreenSignalTrackerTests
{
    [TestMethod]
    public void Direct3DSignal_DoesNotTransferToNewForegroundWindowWhileStale()
    {
        var tracker = new ExclusiveFullscreenSignalTracker();

        Assert.AreEqual(3, tracker.Filter(101, 3));
        Assert.AreEqual(0, tracker.Filter(202, 3));
        Assert.AreEqual(3, tracker.Filter(101, 3));
    }

    [TestMethod]
    public void Direct3DSignal_CanBelongToNewWindowAfterSignalResets()
    {
        var tracker = new ExclusiveFullscreenSignalTracker();

        Assert.AreEqual(3, tracker.Filter(101, 3));
        Assert.AreEqual(5, tracker.Filter(202, 5));
        Assert.AreEqual(3, tracker.Filter(202, 3));
    }

    [TestMethod]
    public void LongRunningDirect3DSignal_RemainsOwnedByOriginalWindow()
    {
        var tracker = new ExclusiveFullscreenSignalTracker();

        for (var sample = 0; sample < 14_400; sample++)
        {
            Assert.AreEqual(3, tracker.Filter(101, 3));
        }
    }
}
