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
    }

    [TestMethod]
    public void Validate_RejectsZeroNotificationDuration()
    {
        var options = IslandMotionOptions.Default with { NotificationVisibleDuration = TimeSpan.Zero };

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(options.Validate);
    }
}
