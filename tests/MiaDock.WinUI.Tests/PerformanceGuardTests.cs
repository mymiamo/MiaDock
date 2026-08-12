namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class PerformanceGuardTests
{
    [TestMethod]
    public void ModuleHosts_CacheViewsInsteadOfRecreatingThemOnEverySwitch()
    {
        foreach (var fileName in new[]
                 {
                     "CompactModuleHost.xaml.cs",
                     "ExpandedModuleHost.xaml.cs",
                     "ModuleNotificationHost.xaml.cs"
                 })
        {
            var source = Read("Controls", fileName);
            StringAssert.Contains(source, "_viewCache");
            StringAssert.Contains(source, "GetOrCreateView");
            StringAssert.Contains(source, "_viewCache.TryGetValue");
        }
    }

    [TestMethod]
    public void CompactHosts_StopAudioSamplingWhenTheirPresentationIsHidden()
    {
        var host = Read("Controls", "CompactModuleHost.xaml.cs");
        var shell = Read("Controls", "IslandShell.xaml.cs");
        var idle = Read("Controls", "IdleCompactView.xaml.cs");
        var media = Read("Controls", "MusicCompactView.xaml.cs");

        StringAssert.Contains(host, "SetHostActive");
        StringAssert.Contains(host, "aware.SetPresentationActive(shouldBeActive)");
        StringAssert.Contains(shell, "_collapsedView.SetHostActive");
        StringAssert.Contains(shell, "_hoverView.SetHostActive");
        StringAssert.Contains(shell, "UpdateHostActivation(transition.CurrentState)");
        StringAssert.Contains(idle, "_isLoaded && _isPresentationActive");
        StringAssert.Contains(media, "IsLoaded && _isPresentationActive");
    }

    [TestMethod]
    public void AnimationCoordinator_UsesOneCancelableSessionAndRejectsStaleCompletion()
    {
        var coordinator = Read("Animations", "IslandAnimationCoordinator.cs");
        var factory = Read("Animations", "ToolkitAnimationFactory.cs");

        StringAssert.Contains(coordinator, "_transitionSequence");
        StringAssert.Contains(coordinator, "_transitionCancellation");
        StringAssert.Contains(coordinator, "RequestModuleTransition");
        StringAssert.Contains(coordinator, "InvalidateActiveTransition");
        Assert.DoesNotContain("_contentCancellation", coordinator, StringComparison.Ordinal);
        Assert.DoesNotContain("_contentSequence", coordinator, StringComparison.Ordinal);
        StringAssert.Contains(coordinator, "ToolkitAnimationFactory.Reset");
        StringAssert.Contains(factory, "cancellationToken.IsCancellationRequested");
        StringAssert.Contains(factory, "StartAsync(element, cancellationToken)");
    }

    [TestMethod]
    public void BoundsAndShell_CoalesceLayoutWorkAndReuseCompositionClip()
    {
        var animator = Read("Animations", "IslandBoundsAnimator.cs");
        var shell = Read("Controls", "IslandShell.xaml.cs");

        StringAssert.Contains(animator, "MinimumFrameDelta");
        StringAssert.Contains(animator, "HasMeaningfulDifference");
        StringAssert.Contains(animator, "apply(to);");
        StringAssert.Contains(animator, "BoundsEasingKind.SoftSpringOut");
        StringAssert.Contains(animator, "EaseOutSoftSpring");
        StringAssert.Contains(shell, "MetricsAreEquivalent");
        StringAssert.Contains(shell, "ClearHardClips");
        StringAssert.Contains(shell, "LayoutRoot.Clip = null;");
        Assert.DoesNotContain("CreateGeometricClip", shell, StringComparison.Ordinal);
    }

    [TestMethod]
    public void StateMotion_UsesToolkitOpacityScaleAndTranslationWithReducedMotionGuard()
    {
        var coordinator = Read("Animations", "IslandAnimationCoordinator.cs");
        var factory = Read("Animations", "ToolkitAnimationFactory.cs");
        var profile = Read("Animations", "IslandAnimationProfile.cs");

        StringAssert.Contains(coordinator, "_animationPreferences.AnimationsEnabled");
        StringAssert.Contains(coordinator, "_options.Preset != MotionPreset.Off");
        StringAssert.Contains(coordinator, "_toolkitAnimations");
        StringAssert.Contains(factory, "AnimationBuilder.Create()");
        StringAssert.Contains(factory, ".Opacity(");
        StringAssert.Contains(factory, ".Scale(");
        StringAssert.Contains(factory, "AnimateShellScaleAsync");
        StringAssert.Contains(factory, "IslandAnimationKind.ScaleFade");
        StringAssert.Contains(factory, "SetIsTranslationEnabled");
        StringAssert.Contains(coordinator, "_options.AnimationKind");
        StringAssert.Contains(coordinator, "AnimateShellScaleAsync");
        StringAssert.Contains(profile, "IsEventMorph");
        StringAssert.Contains(profile, "BoundsEasingFor");
        StringAssert.Contains(coordinator, "isEventMorph");
        StringAssert.Contains(coordinator, "RunDelayedContentTransitionAsync");
        StringAssert.Contains(coordinator, "ContentDelay");
    }

    [TestMethod]
    public void PremiumPolish_UsesCancelableCompositionMotionAndVisibleLoadingFeedback()
    {
        var factory = Read("Animations", "ToolkitAnimationFactory.cs");
        var entrance = Read("Animations", "SettingsEntranceAnimator.cs");
        var music = Read("Controls", "MusicCompactView.xaml.cs");
        var home = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Settings", "HomeSettingsPage.xaml"));
        var media = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Settings", "MediaSettingsPage.xaml"));

        StringAssert.Contains(factory, "AnimateMicroFeedbackAsync");
        StringAssert.Contains(factory, "ConnectedAnimationService");
        StringAssert.Contains(factory, "TryStartConnectedTransition");
        StringAssert.Contains(music, "OnTrackChanged");
        StringAssert.Contains(entrance, "Task.WhenAll(tasks)");
        StringAssert.Contains(entrance, "new UISettings().AnimationsEnabled");
        StringAssert.Contains(home, "IsStoreUpdateChecking");
        StringAssert.Contains(media, "IsMediaLoading");
    }

    [TestMethod]
    public void ThemeRefresh_IsCoalescedAndKeepsCustomOverridesLast()
    {
        var source = Read("Services", "ThemeService.cs");

        StringAssert.Contains(source, "_environmentRefreshPending");
        StringAssert.Contains(source, "Interlocked.Exchange");
        StringAssert.Contains(source, "var styleChanged");
        StringAssert.Contains(source, "resources.Add(_customDictionary)");
    }

    [TestMethod]
    public void ExpandedPointerWork_IsThrottledAndAudioListIsVirtualized()
    {
        var overlay = Read("Windows", "OverlayWindow.xaml.cs");
        var shell = Read("Controls", "IslandShell.xaml.cs");
        var volume = Read("Controls", "VolumeExpandedView.xaml");
        var viewModel = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "ViewModels",
            "VolumeModuleViewModel.cs"));

        StringAssert.Contains(overlay, "PointerActivityThrottleMilliseconds");
        StringAssert.Contains(shell, "ParallaxThrottleMilliseconds");
        StringAssert.Contains(shell, "Vector3.DistanceSquared");
        Assert.DoesNotContain("<ListView", volume, StringComparison.Ordinal);
        Assert.DoesNotContain("<ScrollViewer", volume, StringComparison.Ordinal);
        Assert.DoesNotContain("<ItemsControl", volume, StringComparison.Ordinal);
        var coordinator = Read("Animations", "IslandAnimationCoordinator.cs");
        StringAssert.Contains(coordinator, "StartContentTransition(element, direction, refresh: false)");
        StringAssert.Contains(coordinator, "StartContentTransition(element, MotionDirection.None, refresh: true)");
        StringAssert.Contains(coordinator, "element.InvalidateMeasure()");
        StringAssert.Contains(coordinator, "PrepareViews(incoming, outgoing)");
        StringAssert.Contains(coordinator, "_toolkitAnimations.AnimateTransitionAsync");
        StringAssert.Contains(viewModel, "DispatchToUi");
        StringAssert.Contains(viewModel, "_uiDispatcher.HasThreadAccess");
    }

    private static string Read(string directory, string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, directory, fileName));
}
