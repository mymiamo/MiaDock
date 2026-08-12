using System.Xml.Linq;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class AccessibilityTests
{
    [TestMethod]
    public void TransportButtons_HaveAccessibleNames()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "MediaTransportControls.xaml"));

        var buttons = document.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .ToArray();

        Assert.HasCount(3, buttons);
        Assert.IsTrue(buttons.All(button => button.Attributes()
            .Any(attribute => attribute.Name.LocalName == "AutomationProperties.Name" &&
                              !string.IsNullOrWhiteSpace(attribute.Value))));
    }

    [TestMethod]
    public void Typography_DefinesTextTrimmingForTitlesAndBody()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Themes",
            "Typography.xaml"));

        var trimmingSetters = document.Descendants()
            .Where(element => element.Name.LocalName == "Setter")
            .Count(element => element.Attribute("Property")?.Value == "TextTrimming");

        Assert.IsGreaterThanOrEqualTo(2, trimmingSetters);
    }

    [TestMethod]
    public void HotKeyRecorder_ExposesActionsAndLiveStatusToScreenReaders()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "HotKeyRecorderControl.xaml"));
        var text = document.ToString();
        var buttons = document.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .ToArray();

        Assert.HasCount(3, buttons);
        Assert.IsTrue(buttons.All(button => button.Attributes()
            .Any(attribute => attribute.Name.LocalName == "AutomationProperties.Name" &&
                              !string.IsNullOrWhiteSpace(attribute.Value))));
        StringAssert.Contains(text, "AutomationProperties.HelpText");
        StringAssert.Contains(text, "AutomationProperties.LiveSetting=\"Polite\"");
        StringAssert.Contains(text, "OnRestoreDefaultClicked");
    }

    [TestMethod]
    public void AboutSocialCards_HaveAccessibleNames()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "AboutSettingsPage.xaml"));
        var linkButtons = document.Descendants()
            .Where(element => element.Name.LocalName == "Button" &&
                              element.Attribute("Tag") is not null)
            .ToArray();

        Assert.HasCount(3, linkButtons);
        Assert.IsTrue(linkButtons.All(button => button.Attributes()
            .Any(attribute => attribute.Name.LocalName == "AutomationProperties.Name" &&
                              !string.IsNullOrWhiteSpace(attribute.Value))));
    }
}
