using MiaDock.Platform.Windows.Audio;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class AudioLevelSmootherTests
{
    [TestMethod]
    public void Update_TracksRealPeakWithinVisualBounds()
    {
        var smoother = new AudioLevelSmoother();
        var quiet = smoother.Update(0.05);
        var loud = smoother.Update(0.9);

        Assert.IsTrue(loud.Center > quiet.Center);
        Assert.IsTrue(loud.Left >= 0.18);
        Assert.IsTrue(loud.Left <= 1);
        Assert.IsLessThanOrEqualTo(1, loud.Center);
        Assert.IsLessThanOrEqualTo(1, loud.Right);
    }

    [TestMethod]
    public void Reset_ReturnsSilentMinimum()
    {
        var smoother = new AudioLevelSmoother();
        _ = smoother.Update(1);

        var reset = smoother.Reset();

        Assert.IsFalse(reset.IsAvailable);
        Assert.AreEqual(0.18, reset.Left, 0.0001);
        Assert.AreEqual(0.18, reset.Center, 0.0001);
        Assert.AreEqual(0.18, reset.Right, 0.0001);
    }

    [TestMethod]
    public void Update_TracksRealLeftAndRightChannelsIndependently()
    {
        var smoother = new AudioLevelSmoother();

        var leftHeavy = smoother.Update(0.95, 0.02);

        Assert.IsGreaterThan(leftHeavy.Right, leftHeavy.Left);
        Assert.IsGreaterThanOrEqualTo(leftHeavy.Left, leftHeavy.Center);
    }
}
