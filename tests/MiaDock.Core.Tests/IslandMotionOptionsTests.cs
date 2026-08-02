using MiaDock.Core.Presentation;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class IslandMotionOptionsTests
{
    [TestMethod]
    public void DefaultOptions_AreValidAndEventBased()
    {
        var options = IslandMotionOptions.Default;

        options.Validate();

        Assert.AreEqual(TimeSpan.FromMilliseconds(250), options.PointerExitDelay);
        Assert.AreEqual(TimeSpan.FromSeconds(5), options.NotificationVisibleDuration);
        Assert.AreEqual(TimeSpan.FromSeconds(8), options.ExpandedInactivityDuration);
        Assert.AreEqual(MotionPreset.Balanced, options.Preset);
        Assert.IsTrue(options.Intensity is >= 0 and <= 1);
    }

    [TestMethod]
    public void Validate_RejectsZeroNotificationDuration()
    {
        var options = IslandMotionOptions.Default with { NotificationVisibleDuration = TimeSpan.Zero };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(options.Validate);
    }

    [TestMethod]
    public void Validate_RejectsInvalidAdvancedMotionValues()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            (IslandMotionOptions.Default with { Intensity = double.NaN }).Validate);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            (IslandMotionOptions.Default with { Springiness = 2 }).Validate);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            (IslandMotionOptions.Default with { ContentDelay = TimeSpan.FromSeconds(-1) }).Validate);
    }
}
