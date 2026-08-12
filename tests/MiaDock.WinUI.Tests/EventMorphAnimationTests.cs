namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class EventMorphAnimationTests
{
    [TestMethod]
    public void BoundsAnimator_ExposesSoftSpringEasingForEventMorph()
    {
        var animator = Read("IslandBoundsAnimator.cs");
        var profile = Read("BoundsEasingProfile.cs");
        var animationProfile = Read("IslandAnimationProfile.cs");
        var coordinator = Read("IslandAnimationCoordinator.cs");

        StringAssert.Contains(profile, "SoftSpringOut");
        StringAssert.Contains(animator, "EaseOutSoftSpring");
        StringAssert.Contains(animator, "BoundsEasingProfile");
        StringAssert.Contains(animationProfile, "IsEventMorph");
        StringAssert.Contains(animationProfile, "BoundsEasingFor");
        StringAssert.Contains(animationProfile, "ModuleNotification");
        StringAssert.Contains(coordinator, "isEventMorph");
        StringAssert.Contains(coordinator, "RunDelayedContentTransitionAsync");
        StringAssert.Contains(coordinator, "_options.ContentDelay");
        // Event morph must not rely on shell ScaleTransform as the primary motion.
        StringAssert.Contains(coordinator, "? Task.CompletedTask");
        StringAssert.Contains(coordinator, "AnimateShellScaleAsync");
    }

    private static string Read(string fileName) => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory,
        "Animations",
        fileName));
}
