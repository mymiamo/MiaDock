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
    public void ContentAnimationCancellation_ResetsVisualAndRejectsStaleCompletion()
    {
        var coordinator = Read("Animations", "IslandAnimationCoordinator.cs");
        var factory = Read("Animations", "CompositionAnimationFactory.cs");

        StringAssert.Contains(coordinator, "_contentSequence");
        StringAssert.Contains(coordinator, "sequence == _contentSequence");
        StringAssert.Contains(coordinator, "_contentAnimationTarget");
        StringAssert.Contains(coordinator, "CompositionAnimationFactory.Reset");
        StringAssert.Contains(factory, "catch (OperationCanceledException)");
        StringAssert.Contains(factory, "Reset(visual);");
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

    private static string Read(string directory, string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, directory, fileName));
}
