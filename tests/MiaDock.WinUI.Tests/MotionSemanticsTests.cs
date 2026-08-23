using MiaDock.Core.Presentation;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class MotionSemanticsTests
{
    [TestMethod]
    public void Coordinator_UsesCentralizedOptInForEventMorphAndShellPulse()
    {
        var root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "MiaDock.sln")))
        {
            root = Directory.GetParent(root)?.FullName
                ?? throw new DirectoryNotFoundException("MiaDock solution root was not found.");
        }

        var source = File.ReadAllText(Path.Combine(root, "src", "MiaDock.App", "Animations", "IslandAnimationCoordinator.cs"));
        StringAssert.Contains(source, "MotionSemantics.UsesEventMorph");
        StringAssert.Contains(source, "MotionSemantics.UsesShellPulse");
    }

    [TestMethod]
    public void MotionOptions_KeepMicroInteractionDefaultsEnabled()
    {
        Assert.IsTrue(IslandMotionOptions.Default.EnableIconAnimations);
        Assert.IsTrue(IslandMotionOptions.Default.EnableEventMorph);
    }

    [TestMethod]
    public void MicroInteractionLayer_UsesOneCancelableCompositionSessionAndExistingMotionOptIn()
    {
        var root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "MiaDock.sln")))
        {
            root = Directory.GetParent(root)?.FullName
                ?? throw new DirectoryNotFoundException("MiaDock solution root was not found.");
        }

        var coordinator = File.ReadAllText(Path.Combine(root, "src", "MiaDock.App", "Animations", "MicroInteractionCoordinator.cs"));
        var bridge = File.ReadAllText(Path.Combine(root, "src", "MiaDock.App", "Animations", "MicroMotion.cs"));

        StringAssert.Contains(coordinator, "IAnimationPreferenceService");
        StringAssert.Contains(coordinator, "motion.EnableIconAnimations");
        StringAssert.Contains(coordinator, "CancellationTokenSource");
        StringAssert.Contains(coordinator, "ToolkitAnimationFactory.Reset");
        StringAssert.Contains(coordinator, "target.Visibility != Visibility.Visible");
        StringAssert.Contains(bridge, "PointerPressed");
        StringAssert.Contains(bridge, "OnUnloaded");
    }

    [TestMethod]
    public void MicroAnimationSurfaces_UseCompactMusicArtworkAndStateKeys()
    {
        var root = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(root, "MiaDock.sln")))
        {
            root = Directory.GetParent(root)?.FullName
                ?? throw new DirectoryNotFoundException("MiaDock solution root was not found.");
        }

        var compactMusic = File.ReadAllText(Path.Combine(root, "src", "MiaDock.App", "Controls", "MusicCompactView.xaml"));
        var transport = File.ReadAllText(Path.Combine(root, "src", "MiaDock.App", "Controls", "MediaTransportControls.xaml"));
        var notifications = File.ReadAllText(Path.Combine(root, "src", "MiaDock.App", "Controls", "ModuleNotificationHost.xaml.cs"));

        StringAssert.Contains(compactMusic, "x:Name=\"CompactArtwork\"");
        StringAssert.Contains(compactMusic, "x:Name=\"ArtworkStateOverlay\"");
        StringAssert.Contains(compactMusic, "x:Name=\"TrackChangeGlyph\"");
        StringAssert.Contains(compactMusic, "animations:MicroMotion.StateKey");
        StringAssert.Contains(transport, "animations:MicroMotion.IsInteractive");
        StringAssert.Contains(transport, "animations:MicroMotion.StateKey=\"{Binding PlaybackGlyph}\"");
        StringAssert.Contains(notifications, "MicroMotion.PlayNotification(ViewHost)");
    }
}
