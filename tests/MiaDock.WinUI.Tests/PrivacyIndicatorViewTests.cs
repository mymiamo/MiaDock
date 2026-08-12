namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class PrivacyIndicatorViewTests
{
    [TestMethod]
    public void IdleCompactView_PrivacyDotIgnoresSpeakerAndUsesCentralIndicator()
    {
        var idle = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "IdleCompactView.xaml.cs"));

        StringAssert.Contains(idle, "PrivacyModuleViewModel");
        StringAssert.Contains(idle, "PrivacyIndicatorKind.Camera");
        StringAssert.Contains(idle, "PrivacyIndicatorKind.Microphone");
        StringAssert.Contains(idle, "_cameraBrush");
        StringAssert.Contains(idle, "_microphoneBrush");
        Assert.DoesNotContain("_speakerBrush", idle, StringComparison.Ordinal);
        Assert.DoesNotContain("HasAudioActivity == true", idle, StringComparison.Ordinal);
        Assert.DoesNotContain("Dock.SpeakerInUse", idle, StringComparison.Ordinal);
        Assert.DoesNotContain("MicrophoneUsage == MicrophoneUsageState.Active", idle, StringComparison.Ordinal);
    }

    [TestMethod]
    public void PrivacyViews_ExistAndBindToPrivacyModule()
    {
        var compact = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "PrivacyCompactView.xaml"));
        var expanded = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "PrivacyExpandedView.xaml"));

        StringAssert.Contains(compact, "HasActiveUsage");
        StringAssert.Contains(compact, "EmptyText");
        StringAssert.Contains(expanded, "Applications");
        StringAssert.Contains(expanded, "PrivacyAppIconView");
    }
}
