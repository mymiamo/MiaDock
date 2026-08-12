using MiaDock.Core.Presentation;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class IslandEventMorphProfileTests
{
    [TestMethod]
    public void IsEventMorph_OnlyForNotificationEnterExit()
    {
        // Mirrors IslandAnimationProfile.IsEventMorph — keep in sync with App profile.
        static bool IsEventMorph(IslandTransition transition) =>
            transition.Changed &&
            (transition.CurrentState == IslandVisualState.ModuleNotification ||
             transition.PreviousState == IslandVisualState.ModuleNotification);

        Assert.IsTrue(IsEventMorph(new(
            IslandVisualState.Collapsed,
            IslandVisualState.ModuleNotification,
            IslandTrigger.ModuleEventReceived)));
        Assert.IsTrue(IsEventMorph(new(
            IslandVisualState.Hover,
            IslandVisualState.ModuleNotification,
            IslandTrigger.ModuleEventReceived)));
        Assert.IsTrue(IsEventMorph(new(
            IslandVisualState.ModuleNotification,
            IslandVisualState.Collapsed,
            IslandTrigger.NotificationElapsed)));
        Assert.IsFalse(IsEventMorph(new(
            IslandVisualState.Collapsed,
            IslandVisualState.Hover,
            IslandTrigger.PointerEntered)));
        Assert.IsFalse(IsEventMorph(new(
            IslandVisualState.ExpandedModule,
            IslandVisualState.ExpandedModule,
            IslandTrigger.ModuleEventReceived)));
    }

    [TestMethod]
    public void DefaultMotion_UsesLongerNotificationEnterAndContentDelay()
    {
        Assert.IsGreaterThanOrEqualTo(
            TimeSpan.FromMilliseconds(260),
            IslandMotionOptions.Default.NotificationEnterDuration);
        Assert.IsGreaterThanOrEqualTo(
            TimeSpan.FromMilliseconds(40),
            IslandMotionOptions.Default.ContentDelay);
    }
}
