using System.Xml.Linq;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class DockExperienceViewTests
{
    private static readonly XNamespace XamlNamespace =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [TestMethod]
    public void IdleCompactView_KeepsDateFlexibleBetweenClockAndStatus()
    {
        var document = LoadControl("IdleCompactView.xaml");
        var root = FindNamedElement(document, "LayoutRoot");
        var columns = root.Elements()
            .Single(element => element.Name.LocalName == "Grid.ColumnDefinitions")
            .Elements()
            .ToArray();
        var date = FindNamedElement(document, "DateText");
        var statusTray = FindNamedElement(document, "StatusTray");

        Assert.HasCount(4, columns);
        Assert.AreEqual("*", columns[2].Attribute("Width")?.Value);
        Assert.AreEqual("CharacterEllipsis", date.Attribute("TextTrimming")?.Value);
        Assert.AreEqual("1", date.Attribute("MaxLines")?.Value);
        Assert.AreEqual("3", AttributeValue(statusTray, "Grid.Column"));
    }

    [TestMethod]
    public void BluetoothStatus_UsesUnboxedPresentationLikeNetwork()
    {
        var document = LoadControl("IdleCompactView.xaml");
        var indicator = FindNamedElement(document, "BluetoothStatusIndicator");

        Assert.AreEqual("0", indicator.Attribute("Padding")?.Value);
        Assert.IsNull(indicator.Attribute("Background"));
        Assert.IsNull(indicator.Attribute("CornerRadius"));
    }

    [TestMethod]
    public void IdleCompactView_KeepsMediaMeterRightAndExposesCallIndicator()
    {
        var document = LoadControl("IdleCompactView.xaml");
        var statusTray = FindNamedElement(document, "StatusTray");
        var mediaMeter = FindNamedElement(document, "MusicActivity");
        var callIndicator = FindNamedElement(document, "CallActivityIcon");
        var statusItems = statusTray.Elements().ToArray();

        Assert.AreSame(statusItems[^1], mediaMeter);
        Assert.AreSame(statusItems[1], callIndicator);
        Assert.AreEqual("Collapsed", callIndicator.Attribute("Visibility")?.Value);
        Assert.AreEqual("Olası arama etkinliği",
            AttributeValue(callIndicator, "AutomationProperties.Name"));
    }

    [TestMethod]
    public void MusicCompactView_UsesTrailingMeterAndUnboxedMusicIcon()
    {
        var document = LoadControl("MusicCompactView.xaml");
        var root = FindNamedElement(document, "LayoutRoot");
        var identity = FindNamedElement(document, "MusicIdentity");
        var mediaMeter = FindNamedElement(document, "MusicActivity");
        var musicIcon = FindNamedElement(document, "MusicGlyph");
        var title = FindNamedElement(document, "TrackTitleText");

        Assert.AreEqual("1", AttributeValue(mediaMeter, "Grid.Column"));
        Assert.AreEqual("LeftToRight", root.Attribute("FlowDirection")?.Value);
        Assert.AreSame(identity.Elements()
            .First(element => element.Name.LocalName != "Grid.ColumnDefinitions"), musicIcon);
        Assert.AreEqual("1", AttributeValue(title, "Grid.Column"));
        Assert.IsEmpty(root.Elements()
            .Where(element => element.Name.LocalName == "Border")
            .ToArray());
    }

    [TestMethod]
    public void MusicMetadata_UsesSingleLineEllipsisAndFullTextTooltips()
    {
        foreach (var fileName in new[]
                 {
                     "IdleHoverView.xaml",
                     "MusicCompactView.xaml",
                     "MusicHoverView.xaml"
                 })
        {
            var document = LoadControl(fileName);
            var metadata = document.Descendants()
                .Where(element =>
                    element.Name.LocalName == "TextBlock" &&
                    element.Attribute("Text")?.Value is
                        "{Binding Current.Track.Title}" or "{Binding Current.Track.Artist}")
                .ToArray();

            Assert.IsNotEmpty(metadata, $"{fileName} should expose track metadata.");
            Assert.IsTrue(metadata.All(element =>
                element.Attribute("MaxLines")?.Value == "1" &&
                element.Attribute("TextTrimming")?.Value == "CharacterEllipsis"));
            Assert.IsTrue(metadata.All(element =>
                AttributeValue(element, "ToolTipService.ToolTip") == element.Attribute("Text")?.Value));
        }
    }

    [TestMethod]
    public void HoverTransportControls_AreKeyboardAndScreenReaderReady()
    {
        foreach (var fileName in new[] { "IdleHoverView.xaml", "MusicHoverView.xaml" })
        {
            var document = LoadControl(fileName);
            var buttons = document.Descendants()
                .Where(element => element.Name.LocalName == "Button")
                .ToArray();

            Assert.HasCount(3, buttons, $"{fileName} should expose three transport buttons.");
            Assert.IsTrue(buttons.All(button =>
                button.Attribute("IsTabStop")?.Value == "True" &&
                !string.IsNullOrWhiteSpace(AttributeValue(button, "AutomationProperties.Name")) &&
                !string.IsNullOrWhiteSpace(AttributeValue(button, "AutomationProperties.HelpText")) &&
                !string.IsNullOrWhiteSpace(AttributeValue(button, "ToolTipService.ToolTip"))));
            Assert.IsTrue(buttons.All(button =>
                button.Attribute("Style")?.Value is
                    "{StaticResource IslandCompactIconButtonStyle}" or
                    "{StaticResource DockIconButtonStyle}" &&
                button.Attribute("Background") is null &&
                button.Attribute("CornerRadius") is null));
        }
    }

    [TestMethod]
    public void TimerCompactAndHoverViews_FollowTheActiveTimeTool()
    {
        var compact = LoadControl("TimerCompactView.xaml").ToString();
        var hover = LoadControl("TimerHoverView.xaml").ToString();

        StringAssert.Contains(compact, "CompactTimeText");
        StringAssert.Contains(compact, "CompactStatusText");
        StringAssert.Contains(hover, "CompactPrimaryCommand");
        StringAssert.Contains(hover, "CompactSecondaryCommand");
        StringAssert.Contains(hover, "SilenceAlarmButton");
        StringAssert.Contains(hover, "CompactSecondaryText");
        Assert.DoesNotContain("Command=\"{Binding TimerPrimaryCommand}\"", hover, StringComparison.Ordinal);
    }

    [TestMethod]
    public void TimerCompletionNotification_ProvidesAccessibleSilenceAction()
    {
        var notification = LoadControl("TimerNotificationView.xaml");
        var silenceButton = FindNamedElement(notification, "SilenceAlarmButton");

        Assert.AreEqual("{Binding CompactSecondaryCommand}", AttributeValue(silenceButton, "Command"));
        Assert.AreEqual("True", silenceButton.Attribute("IsTabStop")?.Value);
        Assert.AreEqual(
            "{Binding CompactSecondaryText}",
            AttributeValue(silenceButton, "AutomationProperties.Name"));
        Assert.AreEqual(
            "{Binding CompactSecondaryText}",
            AttributeValue(silenceButton, "AutomationProperties.HelpText"));
        Assert.AreEqual(
            "{Binding CompactSecondaryText}",
            AttributeValue(silenceButton, "ToolTipService.ToolTip"));
    }

    [TestMethod]
    public void GenericViews_ExplainEmptyStateAndProtectDynamicText()
    {
        foreach (var fileName in new[]
                 {
                     "GenericCompactModuleView.xaml",
                     "GenericExpandedModuleView.xaml"
                 })
        {
            var document = LoadControl(fileName);
            var emptyState = FindNamedElement(document, "EmptyState");
            var emptyStateText = FindNamedElement(document, "EmptyStateText");
            var dynamicText = document.Descendants()
                .Where(element =>
                    element.Name.LocalName == "TextBlock" &&
                    element.Attribute("Text")?.Value is
                        "{Binding PrimaryText}" or
                        "{Binding SecondaryText}" or
                        "{Binding ValueText}")
                .ToArray();

            Assert.AreEqual("Etkin olay yok", AttributeValue(emptyState, "AutomationProperties.Name"));
            Assert.AreEqual("Etkin olay yok", emptyStateText.Attribute("Text")?.Value);
            Assert.IsNotEmpty(dynamicText);
            Assert.IsTrue(dynamicText.All(element =>
                element.Attribute("TextTrimming")?.Value == "CharacterEllipsis"));
        }
    }

    [TestMethod]
    public void GenericAndMusicSurfaces_UseSemanticThemeTokens()
    {
        foreach (var fileName in new[]
                 {
                     "MusicCompactView.xaml",
                     "MusicHoverView.xaml",
                     "GenericCompactModuleView.xaml",
                     "GenericExpandedModuleView.xaml"
                 })
        {
            var document = LoadControl(fileName);
            var semanticPaints = document.Descendants()
                .Attributes()
                .Where(attribute => attribute.Name.LocalName is
                    "Background" or "BorderBrush" or "Fill" or "Foreground")
                .Select(attribute => attribute.Value)
                .ToArray();
            var sharedDockStyles = document.Descendants()
                .Attributes("Style")
                .Select(attribute => attribute.Value)
                .Where(value => value.StartsWith(
                    "{StaticResource Dock",
                    StringComparison.Ordinal))
                .ToArray();

            Assert.IsTrue(
                semanticPaints.Length > 0 || sharedDockStyles.Length > 0,
                $"{fileName} should consume semantic paint or a shared dock style.");
            Assert.IsTrue(semanticPaints.All(value =>
                    value.StartsWith("{ThemeResource ", StringComparison.Ordinal) ||
                    value.StartsWith("{Binding ", StringComparison.Ordinal)),
                $"{fileName} contains a literal semantic color.");
        }
    }

    [TestMethod]
    public void TimerExpandedView_UsesToggleButtonCompatibleSegmentStyle()
    {
        var document = LoadControl("TimerExpandedView.xaml");
        var segments = document.Descendants()
            .Where(element => element.Name.LocalName == "ToggleButton")
            .ToArray();

        Assert.HasCount(2, segments);
        Assert.IsTrue(segments.All(segment =>
            segment.Attribute("Style")?.Value ==
                "{StaticResource DockSegmentToggleButtonStyle}"));
        Assert.IsFalse(segments.Any(segment =>
            segment.Attribute("Style")?.Value ==
                "{StaticResource DockInlineButtonStyle}"));

        var numberBoxes = document.Descendants()
            .Where(element => element.Name.LocalName == "NumberBox")
            .ToArray();
        Assert.HasCount(3, numberBoxes);
        Assert.IsTrue(numberBoxes.All(box =>
            box.Attribute("SpinButtonPlacementMode")?.Value == "Inline"));
        Assert.IsTrue(numberBoxes.All(box =>
            box.Attribute("GotFocus")?.Value == "OnDurationEditorGotFocus" &&
            box.Attribute("LostFocus")?.Value == "OnDurationEditorLostFocus"));
    }

    [TestMethod]
    public void ExpandedHost_ReservesTheFullSwitcherHeight()
    {
        var document = LoadControl("ExpandedModuleHost.xaml");
        var rowDefinitions = document.Descendants()
            .Where(element => element.Name.LocalName == "RowDefinition")
            .ToArray();
        var switcher = FindNamedElement(document, "Switcher");

        Assert.AreEqual("64", rowDefinitions[^1].Attribute("Height")?.Value);
        Assert.AreEqual("12,6,12,10", switcher.Attribute("Margin")?.Value);
        Assert.AreEqual("Stretch", switcher.Attribute("HorizontalAlignment")?.Value);
        Assert.IsNull(switcher.Attribute("Width"));
        Assert.IsNull(switcher.Attribute("MaxWidth"));
    }

    [TestMethod]
    public void ModuleVariants_UseTheSharedDockHierarchy()
    {
        foreach (var fileName in new[]
                 {
                     "MusicCompactView.xaml",
                     "VolumeCompactView.xaml",
                     "SystemActivityCompactView.xaml",
                     "BatteryCompactView.xaml",
                     "NetworkCompactView.xaml",
                     "BluetoothCompactView.xaml",
                     "TimerCompactView.xaml",
                     "TransferCompactView.xaml"
                 })
        {
            StringAssert.Contains(
                LoadControl(fileName).ToString(),
                "DockCompactModuleLayoutStyle",
                $"{fileName} should use the shared compact hierarchy.");
        }

        foreach (var fileName in new[]
                 {
                     "ExpandedMusicView.xaml",
                     "VolumeExpandedView.xaml",
                     "SystemActivityExpandedView.xaml",
                     "BatteryExpandedView.xaml",
                     "NetworkExpandedView.xaml",
                     "BluetoothExpandedView.xaml",
                     "TransferExpandedView.xaml",
                     "GenericExpandedModuleView.xaml"
                 })
        {
            var text = LoadControl(fileName).ToString();
            Assert.IsTrue(
                text.Contains("DockExpandedModuleLayoutStyle", StringComparison.Ordinal) &&
                (text.Contains("DockTitleTextStyle", StringComparison.Ordinal) ||
                 text.Contains("DockSectionStyle", StringComparison.Ordinal)),
                $"{fileName} should use the shared expanded hierarchy.");
        }

        foreach (var fileName in new[]
                 {
                     "TrackNotificationView.xaml",
                     "VolumeNotificationView.xaml",
                     "TimerNotificationView.xaml",
                     "TransferNotificationView.xaml",
                     "NotificationModuleNotificationView.xaml",
                     "GenericModuleNotificationView.xaml",
                     "StoreUpdateNotificationView.xaml"
                 })
        {
            StringAssert.Contains(
                LoadControl(fileName).ToString(),
                "DockNotificationLayoutStyle",
                $"{fileName} should use the shared notification hierarchy.");
        }
    }

    [TestMethod]
    public void SharedModuleActions_ProvideAccessibleTargetsAndExplanations()
    {
        var transport = LoadControl("MediaTransportControls.xaml");
        var buttons = transport.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .ToArray();

        Assert.HasCount(3, buttons);
        Assert.IsTrue(buttons.All(button =>
            button.Attribute("Width")?.Value == "44" &&
            button.Attribute("Height")?.Value == "44" &&
            button.Attribute("Style")?.Value == "{StaticResource DockIconButtonStyle}" &&
            !string.IsNullOrWhiteSpace(AttributeValue(button, "AutomationProperties.HelpText"))));

        var bluetooth = LoadControl("BluetoothExpandedView.xaml");
        var deviceName = bluetooth.Descendants().Single(element =>
            element.Name.LocalName == "TextBlock" &&
            element.Attribute("Text")?.Value == "{Binding DisplayName}");
        Assert.AreEqual("CharacterEllipsis", deviceName.Attribute("TextTrimming")?.Value);
        Assert.AreEqual(
            "{Binding DisplayName}",
            AttributeValue(deviceName, "ToolTipService.ToolTip"));
    }

    private static XElement FindNamedElement(XDocument document, string name) =>
        document.Descendants().Single(element =>
            element.Attribute(XamlNamespace + "Name")?.Value == name);

    private static string? AttributeValue(XElement element, string localName) =>
        element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == localName)?.Value;

    private static XDocument LoadControl(string fileName) => XDocument.Load(
        Path.Combine(AppContext.BaseDirectory, "Controls", fileName));
}
