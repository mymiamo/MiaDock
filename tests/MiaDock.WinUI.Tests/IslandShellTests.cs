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
    public void ExpandedContent_RemainsInsideTheMainDockSurface()
    {
        var document = LoadControl("IslandShell.xaml");
        var surface = document.Descendants().Single(element =>
            element.Attribute(XName.Get(
                "Name",
                "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "Surface");
        var contentHost = surface.Descendants().Single(element =>
            element.Attribute(XName.Get(
                "Name",
                "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "ContentHost");

        Assert.AreEqual("Grid", contentHost.Name.LocalName);
        Assert.AreEqual(1, document.Descendants().Count(element =>
            element.Name.LocalName == "Border" &&
            element.Attribute(XName.Get(
                "Name",
                "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "Surface"));
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
    public void IslandSurface_UsesSharedProfessionalDockChrome()
    {
        var document = LoadControl("IslandShell.xaml");
        var surface = document.Descendants().Single(element =>
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "Surface");

        Assert.AreEqual(
            "{StaticResource DockSurfaceStyle}",
            surface.Attribute("Style")?.Value);
        Assert.IsNull(surface.Attribute("Background"));
        Assert.IsNull(surface.Attribute("BorderBrush"));
    }

    [TestMethod]
    public void IslandSurface_UsesRoundedSystemBackdropBehindContent()
    {
        var document = LoadControl("IslandShell.xaml");
        var layoutChildren = document.Descendants().Single(element =>
                element.Attribute(XName.Get(
                    "Name",
                    "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "LayoutRoot")
            .Elements()
            .ToArray();
        var backdrop = layoutChildren.Single(element =>
            element.Attribute(XName.Get(
                "Name",
                "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "BackdropSurface");
        var surface = layoutChildren.Single(element =>
            element.Attribute(XName.Get(
                "Name",
                "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "Surface");

        Assert.AreEqual("SystemBackdropElement", backdrop.Name.LocalName);
        Assert.AreEqual("23", backdrop.Attribute("CornerRadius")?.Value);
        Assert.AreEqual(0, Array.IndexOf(layoutChildren, backdrop));
        Assert.AreEqual(1, Array.IndexOf(layoutChildren, surface));
    }

    [TestMethod]
    public void IslandSurface_RemovesOuterStrokeAcrossThemes()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "IslandShell.xaml.cs"));
        var styles = XDocument.Load(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MiaDock.UI",
            "Themes",
            "DockControlStyles.xaml"));
        var xNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
        var surfaceStyle = styles.Descendants().Single(element =>
            element.Name.LocalName == "Style" &&
            element.Attribute(xNamespace + "Key")?.Value == "DockSurfaceStyle");
        var borderThickness = surfaceStyle.Elements().Single(element =>
            element.Name.LocalName == "Setter" &&
            element.Attribute("Property")?.Value == "BorderThickness");
        var overlaySource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MiaDock.App",
            "OverlayWindow.xaml.cs"));
        var controllerSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MiaDock.Platform.Windows",
            "Overlay",
            "OverlayWindowController.cs"));

        StringAssert.Contains(source, "Surface.BorderThickness = new Thickness(0);");
        Assert.DoesNotContain(
            "Surface.BorderThickness = new Thickness(1)",
            source,
            StringComparison.Ordinal);
        Assert.AreEqual("0", borderThickness.Attribute("Value")?.Value);
        Assert.DoesNotContain("ToLayeredSurfaceArgb", overlaySource, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "new LayeredRoundedBackdropWindow",
            controllerSource,
            StringComparison.Ordinal);
        StringAssert.Contains(controllerSource, "ClearWindowRegionIfNeeded");
        Assert.DoesNotContain(
            "RoundedRegionBuilder.Create(",
            controllerSource,
            StringComparison.Ordinal);
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
        Assert.AreEqual(
            "{ThemeResource IslandTextPrimaryBrush}",
            musicActivity.Attribute("Foreground")?.Value);
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
    public void IdleExpandedView_ShowsDashboardStatusAndMediaControls()
    {
        var document = LoadControl("IdleExpandedView.xaml");
        var transport = LoadControl("MediaTransportControls.xaml");
        var names = document.Descendants()
            .Attributes(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))
            .Select(attribute => attribute.Value)
            .ToHashSet(StringComparer.Ordinal);
        var commands = transport.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Select(element => element.Attribute("Command")?.Value)
            .ToArray();

        foreach (var name in new[]
                 {
                     "TimeText", "DateText", "NetworkCard", "BatteryCard",
                     "BluetoothCard", "SystemCard", "FocusCard", "MusicPanel",
                     "MediaArtwork", "EmptyArtwork", "MediaMetadataPanel", "MediaEmptyPanel"
                 })
        {
            Assert.IsTrue(names.Contains(name), $"{name} bulunamadı.");
        }

        CollectionAssert.Contains(commands, "{Binding PreviousCommand}");
        CollectionAssert.Contains(commands, "{Binding PlayPauseCommand}");
        CollectionAssert.Contains(commands, "{Binding NextCommand}");
        Assert.IsTrue(document.Descendants().Any(element =>
            element.Attribute("Text")?.Value == "{Binding Current.Track.Title}"));
        Assert.IsTrue(document.Descendants().Any(element =>
            element.Name.LocalName == "MediaTimeline"));
        Assert.IsTrue(document.Descendants().Any(element =>
            element.Name.LocalName == "AudioActivityIndicator"));
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
        var rows = host.Descendants()
            .Where(element => element.Name.LocalName == "RowDefinition")
            .ToArray();
        var switcherHost = host.Descendants().Single(element =>
            element.Name.LocalName == "ModuleSwitcher");
        var switcher = LoadControl("ModuleSwitcher.xaml");

        var reservedHeight = double.Parse(
            rows[^1].Attribute("Height")!.Value,
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.AreEqual("2", switcherHost.Attribute("Grid.Row")?.Value);
        Assert.AreEqual("Stretch", switcherHost.Attribute("HorizontalAlignment")?.Value);
        Assert.IsNull(switcherHost.Attribute("Width"));
        Assert.IsNull(switcherHost.Attribute("MaxWidth"));
        Assert.IsGreaterThanOrEqualTo(64, reservedHeight);
        Assert.IsTrue(switcher.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .All(element =>
                element.Attribute("Width")?.Value == "44" &&
                element.Attribute("Height")?.Value == "44"));
        Assert.IsTrue(switcher.Descendants().Any(element =>
            element.Name.LocalName == "ScrollViewer" &&
            element.Attribute("HorizontalScrollMode")?.Value == "Enabled"));
    }

    [TestMethod]
    public void ExpandedDock_UsesBottomNavigationAndSharedActiveCenterHierarchy()
    {
        var switcher = LoadControl("ModuleSwitcher.xaml");
        var rootGrid = switcher.Descendants()
            .First(element => element.Name.LocalName == "Grid" &&
                              element.Elements().Any(child => child.Name.LocalName == "ScrollViewer"));
        var scrollViewer = rootGrid.Elements()
            .Single(element => element.Name.LocalName == "ScrollViewer");
        var navigationButtons = rootGrid.Elements()
            .Where(element => element.Name.LocalName == "Button")
            .ToArray();

        Assert.AreEqual("1", scrollViewer.Attribute("Grid.Column")?.Value);
        Assert.HasCount(2, navigationButtons);
        Assert.IsTrue(navigationButtons.Any(button => button.Attribute("Grid.Column") is null));
        Assert.IsTrue(navigationButtons.Any(button => button.Attribute("Grid.Column")?.Value == "2"));

        var idle = LoadControl("IdleExpandedView.xaml");
        var styledCards = idle.Descendants()
            .Attributes("Style")
            .Select(attribute => attribute.Value)
            .ToArray();
        Assert.IsGreaterThanOrEqualTo(
            2,
            styledCards.Count(value => value == "{StaticResource DockSectionStyle}"));
        Assert.IsFalse(styledCards.Contains("{StaticResource IslandExpandedMetricCardStyle}"));

        var music = LoadControl("ExpandedMusicView.xaml");
        Assert.IsTrue(music.Descendants().Any(element =>
            element.Name.LocalName == "MediaTimeline" &&
            element.Attribute("Grid.ColumnSpan") is null));
    }

    [TestMethod]
    public void ExpandedHost_DeactivatesSamplingWhenRemovedFromVisualTree()
    {
        var host = LoadControl("ExpandedModuleHost.xaml");
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "ExpandedModuleHost.xaml.cs"));

        Assert.AreEqual("OnLoaded", host.Root?.Attribute("Loaded")?.Value);
        Assert.AreEqual("OnUnloaded", host.Root?.Attribute("Unloaded")?.Value);
        StringAssert.Contains(source, "_isHostRequestedActive && IsLoaded");
        StringAssert.Contains(source, "aware.SetPresentationActive(false)");
    }

    [TestMethod]
    public void SystemActivityView_ContainsCallStatusWithoutPrivacyDeviceRows()
    {
        var document = LoadControl("SystemActivityExpandedView.xaml");
        var sliders = document.Descendants()
            .Where(element => element.Name.LocalName == "Slider")
            .ToArray();

        Assert.IsEmpty(sliders);
        var text = document.ToString();
        StringAssert.Contains(text, "CallText");
        StringAssert.Contains(text, "Arama durumu");
        Assert.DoesNotContain("MicrophoneText", text, StringComparison.Ordinal);
        Assert.DoesNotContain("CameraText", text, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenMicrophonePrivacySettingsCommand", text, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenCameraPrivacySettingsCommand", text, StringComparison.Ordinal);
        Assert.DoesNotContain("IsCameraInUse", text, StringComparison.Ordinal);
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
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "DefaultButton"));
        Assert.IsTrue(document.Descendants().Any(element =>
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "ModuleButtons"));
    }

    [TestMethod]
    public void ModuleSwitcher_UpdatesSelectionWithoutRebuildingTheVisualTree()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MiaDock.App",
            "Controls",
            "ModuleSwitcher.xaml.cs"));

        StringAssert.Contains(source, "OnSelectionChanged");
        StringAssert.Contains(source, "UpdateSelectionVisuals");
        StringAssert.Contains(source, "OnModulesChanged");
        Assert.IsFalse(source.Contains("new PropertyMetadata(null, OnDataChanged)", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ExpandedModuleHost_FallsBackInsteadOfCrashingOnViewLoadFailure()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MiaDock.App",
            "Controls",
            "ExpandedModuleHost.xaml.cs"));

        StringAssert.Contains(source, "catch (Exception exception)");
        StringAssert.Contains(source, "ModuleViewLoadFailedEventArgs");
        StringAssert.Contains(source, "CreateSafeFallbackView");

        foreach (var host in new[] { "CompactModuleHost.xaml.cs", "ModuleNotificationHost.xaml.cs" })
        {
            var hostSource = File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                "src",
                "MiaDock.App",
                "Controls",
                host));
            StringAssert.Contains(hostSource, "ModuleViewLoadFailedEventArgs");
            StringAssert.Contains(hostSource, "catch (Exception exception)");
        }
    }

    [TestMethod]
    public void ExpandedShell_UsesModuleMinimumHeightAndRoundedCompositionClip()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "IslandShell.xaml.cs"));

        StringAssert.Contains(source, "Descriptor.MinimumExpandedHeight");
        StringAssert.Contains(source, "Math.Clamp(Math.Max(baseLayout.ExpandedHeight, minimum), 360, 420)");
        StringAssert.Contains(source, "RequestLayoutTransition(effective)");
        StringAssert.Contains(source, "BackdropSurface.CornerRadius = cornerRadius;");
        StringAssert.Contains(source, "Surface.CornerRadius = cornerRadius;");
    }

    [TestMethod]
    public void RedesignedDockStates_ConsumeTheSharedDesignSystem()
    {
        var compact = LoadControl("IdleCompactView.xaml").ToString();
        var hover = LoadControl("IdleHoverView.xaml").ToString();
        var expanded = LoadControl("IdleExpandedView.xaml").ToString();

        StringAssert.Contains(compact, "DockTitleTextStyle");
        StringAssert.Contains(compact, "DockStatusBadgeStyle");
        StringAssert.Contains(hover, "DockIconButtonStyle");
        StringAssert.Contains(hover, "Width=\"44\"");
        StringAssert.Contains(hover, "Height=\"44\"");
        StringAssert.Contains(expanded, "DockDisplayTextStyle");
        StringAssert.Contains(expanded, "DockSectionStyle");
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

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MiaDock.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
