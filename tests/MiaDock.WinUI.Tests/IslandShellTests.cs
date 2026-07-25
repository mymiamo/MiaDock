using System.Xml.Linq;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class IslandShellTests
{
    [TestMethod]
    public void IslandShell_ProvidesNamedLayoutAndContentHosts()
    {
        var document = LoadControl("IslandShell.xaml");
        var names = document.Descendants()
            .Attributes(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))
            .Select(attribute => attribute.Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.IsTrue(names.Contains("LayoutRoot"));
        Assert.IsTrue(names.Contains("Surface"));
        Assert.IsTrue(names.Contains("ContentHost"));
    }

    [TestMethod]
    public void EveryControlXaml_IsWellFormed()
    {
        var controlDirectory = Path.Combine(AppContext.BaseDirectory, "Controls");
        var files = Directory.GetFiles(controlDirectory, "*.xaml");

        Assert.IsGreaterThanOrEqualTo(10, files.Length);
        foreach (var file in files)
        {
            _ = XDocument.Load(file);
        }
    }

    [TestMethod]
    public void IslandSurface_MatchesReferenceCapsuleChrome()
    {
        var document = LoadControl("IslandShell.xaml");
        var surface = document.Descendants().Single(element =>
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "Surface");

        Assert.AreEqual("1", surface.Attribute("BorderThickness")?.Value);
        Assert.AreEqual("#FF252525", surface.Attribute("BorderBrush")?.Value);
        Assert.AreEqual("23", surface.Attribute("CornerRadius")?.Value);
    }

    [TestMethod]
    public void MusicCompactView_UsesReusableAudioActivityIndicator()
    {
        var document = LoadControl("MusicCompactView.xaml");
        var indicator = document.Descendants().Single(element =>
            element.Name.LocalName == "AudioActivityIndicator");

        Assert.AreEqual("{Binding LeftAudioLevel}", indicator.Attribute("LeftLevel")?.Value);
        Assert.AreEqual("{Binding IsAudioLevelAvailable}", indicator.Attribute("IsAudioAvailable")?.Value);
        Assert.IsFalse(document.Descendants().Any(element => element.Name.LocalName == "Storyboard"));
    }

    [TestMethod]
    public void AudioActivityIndicator_HasRealLevelBarsAndFallbackAnimation()
    {
        var document = LoadControl("AudioActivityIndicator.xaml");
        var names = document.Descendants()
            .Attributes(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))
            .Select(attribute => attribute.Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.IsTrue(names.Contains("FallbackStoryboard"));
        Assert.IsTrue(names.Contains("LeftScale"));
        Assert.IsTrue(names.Contains("CenterScale"));
        Assert.IsTrue(names.Contains("RightScale"));
        Assert.IsTrue(document.Descendants()
            .Where(element => element.Name.LocalName == "Rectangle")
            .All(element => element.Attribute("Width")?.Value == "2"));
    }

    [TestMethod]
    public void IdleClock_HasIndependentPulseAndMusicActivityIndicator()
    {
        var document = LoadControl("IdleCompactView.xaml");
        var names = document.Descendants()
            .Attributes(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))
            .Select(attribute => attribute.Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.IsTrue(names.Contains("IdlePulseStoryboard"));
        Assert.IsTrue(names.Contains("MusicActivity"));
        Assert.IsTrue(names.Contains("ActivityDot"));

        var statusTray = document.Descendants().Single(element =>
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "StatusTray");
        var statusItems = statusTray.Elements().ToArray();
        var network = statusItems.First();
        var musicActivity = statusItems.Last();
        Assert.AreEqual("FontIcon", network.Name.LocalName);
        Assert.AreEqual("{Binding NetworkStatusBrush}", network.Attribute("Foreground")?.Value);
        Assert.AreEqual("CallActivityIcon",
            statusItems[1].Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value);
        Assert.AreEqual("AudioActivityIndicator", musicActivity.Name.LocalName);
        Assert.AreEqual("14", musicActivity.Attribute("Height")?.Value);
        Assert.AreEqual("White", musicActivity.Attribute("Foreground")?.Value);
    }

    [TestMethod]
    public void IdleHoverView_HostsMusicAwareHoverContent()
    {
        var document = LoadControl("IdleHoverView.xaml");
        var names = document.Descendants()
            .Attributes(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))
            .Select(attribute => attribute.Value)
            .ToHashSet(StringComparer.Ordinal);
        var commands = document.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Select(element => element.Attribute("Command")?.Value)
            .ToArray();

        Assert.IsTrue(names.Contains("TimeText"));
        Assert.IsTrue(names.Contains("DateText"));
        Assert.IsTrue(names.Contains("MusicRow"));
        CollectionAssert.Contains(commands, "{Binding PreviousCommand}");
        CollectionAssert.Contains(commands, "{Binding PlayPauseCommand}");
        CollectionAssert.Contains(commands, "{Binding NextCommand}");
        Assert.IsTrue(document.Descendants().Any(element =>
            element.Attribute("Text")?.Value == "{Binding Current.Track.Title}"));
    }

    [TestMethod]
    public void ModuleHosts_UseDynamicContentControls()
    {
        foreach (var fileName in new[]
                 {
                     "CompactModuleHost.xaml",
                     "ExpandedModuleHost.xaml",
                     "ModuleNotificationHost.xaml"
                 })
        {
            var document = LoadControl(fileName);
            Assert.IsTrue(document.Descendants().Any(element => element.Name.LocalName == "ContentControl"));
        }
    }

    [TestMethod]
    public void ModuleSwitcher_ExposesKeyboardWheelAndTouchInput()
    {
        var document = LoadControl("ModuleSwitcher.xaml");
        var root = document.Root!;

        Assert.IsNotNull(root.Attribute("KeyDown"));
        Assert.IsNotNull(root.Attribute("PointerWheelChanged"));
        Assert.IsNotNull(root.Attribute("ManipulationCompleted"));
        Assert.AreEqual("TranslateX", root.Attribute("ManipulationMode")?.Value);
    }

    [TestMethod]
    public void ExpandedHost_ReservesSwitcherHeightWithoutClipping()
    {
        var host = LoadControl("ExpandedModuleHost.xaml");
        var firstRow = host.Descendants()
            .Where(element => element.Name.LocalName == "RowDefinition")
            .First();
        var switcher = LoadControl("ModuleSwitcher.xaml");
        var buttonHeights = switcher.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Select(element => double.Parse(
                element.Attribute("Height")!.Value,
                System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();

        var reservedHeight = double.Parse(
            firstRow.Attribute("Height")!.Value,
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.IsGreaterThanOrEqualTo(buttonHeights.Max() + 4, reservedHeight);
        Assert.IsTrue(switcher.Descendants().Any(element =>
            element.Name.LocalName == "ScrollViewer" &&
            element.Attribute("HorizontalScrollMode")?.Value == "Enabled"));
    }

    [TestMethod]
    public void SystemActivityView_ExposesMasterAndApplicationVolumeAccessibly()
    {
        var document = LoadControl("SystemActivityExpandedView.xaml");
        var sliders = document.Descendants()
            .Where(element => element.Name.LocalName == "Slider")
            .ToArray();

        Assert.HasCount(2, sliders);
        Assert.IsTrue(sliders.All(slider => slider.Attributes().Any(attribute =>
            attribute.Name.LocalName == "AutomationProperties.Name")));
        Assert.IsTrue(sliders.Any(slider =>
            slider.Attribute("IsEnabled")?.Value == "{Binding IsApplicationVolumeAvailable}"));
    }

    [TestMethod]
    public void IdleCompactView_ShowsClockDateAndDeviceSummaryAccessibly()
    {
        var document = LoadControl("IdleCompactView.xaml");
        var names = document.Descendants()
            .Attributes(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))
            .Select(attribute => attribute.Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.IsTrue(names.Contains("LayoutRoot"));
        Assert.IsTrue(names.Contains("TimeText"));
        Assert.IsTrue(names.Contains("DateText"));
        Assert.IsTrue(names.Contains("StatusTray"));
        var bindings = document.Descendants().Attributes()
            .Select(attribute => attribute.Value)
            .ToArray();
        CollectionAssert.Contains(bindings, "{Binding NetworkStatus}");
        CollectionAssert.Contains(bindings, "{Binding BluetoothStatus}");
        CollectionAssert.Contains(bindings, "{Binding BatteryStatus}");
        Assert.IsTrue(document.Descendants().Any(element =>
            element.Name.LocalName == "Grid" &&
            element.Attributes().Any(attribute =>
                attribute.Name.LocalName == "AutomationProperties.LiveSetting" &&
                attribute.Value == "Polite")));
    }

    [TestMethod]
    public void MusicHoverAndExpandedViews_ExposePlaybackControls()
    {
        var hover = LoadControl("MusicHoverView.xaml");
        var hoverCommands = hover.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Select(element => element.Attribute("Command")?.Value)
            .ToArray();
        CollectionAssert.Contains(hoverCommands, "{Binding PreviousCommand}");
        CollectionAssert.Contains(hoverCommands, "{Binding PlayPauseCommand}");
        CollectionAssert.Contains(hoverCommands, "{Binding NextCommand}");

        var expanded = LoadControl("ExpandedMusicView.xaml");
        Assert.IsTrue(expanded.Descendants().Any(element =>
            element.Name.LocalName == "MediaTransportControls" &&
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "TransportControls"));
    }

    [TestMethod]
    public void ModuleSwitcher_ContainsDirectIconButtonHost()
    {
        var document = LoadControl("ModuleSwitcher.xaml");
        Assert.IsTrue(document.Descendants().Any(element =>
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "ModuleButtons"));
    }

    [TestMethod]
    public void DeviceStatusViews_ArePresentAndNetworkRatesAreAccessible()
    {
        foreach (var fileName in new[]
                 {
                     "BatteryCompactView.xaml", "BatteryExpandedView.xaml",
                     "NetworkCompactView.xaml", "NetworkExpandedView.xaml",
                     "BluetoothCompactView.xaml", "BluetoothExpandedView.xaml"
                 })
        {
            _ = LoadControl(fileName);
        }

        var network = LoadControl("NetworkExpandedView.xaml");
        var automationNames = network.Descendants().Attributes()
            .Where(attribute => attribute.Name.LocalName == "AutomationProperties.Name")
            .Select(attribute => attribute.Value)
            .ToArray();
        CollectionAssert.Contains(automationNames, "İndirme hızı");
        CollectionAssert.Contains(automationNames, "Yükleme hızı");
    }

    private static XDocument LoadControl(string fileName) => XDocument.Load(
        Path.Combine(AppContext.BaseDirectory, "Controls", fileName));
}
