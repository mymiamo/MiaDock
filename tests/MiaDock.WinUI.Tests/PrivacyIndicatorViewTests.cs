namespace MiaDock.WinUI.Tests;

using System.Xml.Linq;

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

    [TestMethod]
    public void PrivacyExpandedView_KeepsAppsVisibleAndMovesSettingsIntoCompactHero()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "PrivacyExpandedView.xaml"));
        var source = document.ToString();
        var hero = document.Descendants().Single(element =>
            element.Name.LocalName == "Border" &&
            element.Attribute("Style")?.Value == "{StaticResource DockHeroSectionStyle}");

        Assert.HasCount(2, document.Descendants()
            .Where(element => element.Name.LocalName == "RowDefinition")
            .ToArray());
        Assert.HasCount(2, hero.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .ToArray());
        StringAssert.Contains(source, "HeroDetailText");
        StringAssert.Contains(source, "DockMiniGroupedRowStyle");
        Assert.DoesNotContain("DockStatusBadgeStyle", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Grid.Row=\"2\"", source, StringComparison.Ordinal);
        Assert.AreEqual(1, document.Descendants().Count(element =>
            element.Name.LocalName == "TextBlock" &&
            element.Attribute("Text")?.Value == "{Binding SummaryText}"));
    }
}
