using System.Xml.Linq;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class SettingsWindowTests
{
    [TestMethod]
    public void SettingsWindow_ContainsAllNavigationCategories()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "SettingsWindow.xaml"));
        var tags = document.Descendants()
            .Where(element => element.Name.LocalName == "NavigationViewItem")
            .Select(element => element.Attribute("Tag")?.Value)
            .ToArray();

        CollectionAssert.AreEquivalent(
            new[] { "general", "modules", "appearance", "media", "time", "notifications", "fullscreen", "monitor", "tray", "startup", "diagnostics", "about" },
            tags);
    }

    [TestMethod]
    public void SettingsWindow_UsesCompactRailAndSearchNavigation()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "SettingsWindow.xaml"));
        var text = document.ToString();

        StringAssert.Contains(text, "CompactPaneLength=\"60\"");
        StringAssert.Contains(text, "PlaceholderText=\"Ayarlarda ara\"");
        StringAssert.Contains(text, "Background=\"#171B2B\"");
    }

    [TestMethod]
    public void NotificationPage_RequiresExplicitBodyOptIn()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "NotificationSettingsPage.xaml"));
        var text = document.ToString();

        StringAssert.Contains(text, "OnEnableToggled");
        StringAssert.Contains(text, "Gövde metni her uygulama için ayrıca açılmalıdır");
        StringAssert.Contains(text, "NotificationsInFullscreen");
        StringAssert.Contains(text, "NotificationUseAllowList");
    }

    [TestMethod]
    public void AppearancePage_ExplainsTransparentGlassAndGroupsControls()
    {
        var text = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "AppearanceSettingsPage.xaml")).ToString();

        StringAssert.Contains(text, "IsBlurredGlassTheme");
        StringAssert.Contains(text, "IsBackgroundColorEditable");
        StringAssert.Contains(text, "Saydam Bulanık Cam");
        StringAssert.Contains(text, "Ada boyutları");
        StringAssert.Contains(text, "Yüzey ve renk");
        StringAssert.Contains(text, "Hareket");
    }

    [TestMethod]
    public void FullscreenPage_DoesNotOfferAutomaticTrackExpansion()
    {
        var text = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "FullscreenSettingsPage.xaml")).ToString();

        Assert.DoesNotContain("ShowTrackChanges", text, StringComparison.Ordinal);
        StringAssert.Contains(text, "Şarkı değişimi dock'u kendiliğinden genişletmez.");
    }

    [TestMethod]
    public void DevelopmentManifest_DeclaresNotificationListenerCapability()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Package.appxmanifest"));
        var capabilityNames = document.Descendants()
            .Where(element => element.Name.LocalName == "Capability")
            .Select(element => element.Attribute("Name")?.Value)
            .ToArray();

        CollectionAssert.Contains(capabilityNames, "userNotificationListener");
        CollectionAssert.Contains(capabilityNames, "runFullTrust");
        var packageNamespace = document.Root!.GetDefaultNamespace();
        var identity = document.Root.Element(packageNamespace + "Identity");
        var properties = document.Root.Element(packageNamespace + "Properties");

        Assert.AreEqual("mymiamo.net.MiaDock", identity?.Attribute("Name")?.Value);
        Assert.AreEqual(
            "CN=FAC642FD-F594-4E90-B1DB-38F94EA36BCA",
            identity?.Attribute("Publisher")?.Value);
        Assert.AreEqual("1.0.0.0", identity?.Attribute("Version")?.Value);
        Assert.AreEqual(
            "Eray Durupınar (mymiamo.net)",
            properties?.Element(packageNamespace + "PublisherDisplayName")?.Value);
        var manifestText = document.ToString();
        StringAssert.Contains(manifestText, @"Assets\StoreLogo.png");
        StringAssert.Contains(manifestText, @"Assets\Square44x44Logo.png");
        StringAssert.Contains(manifestText, @"Assets\Square150x150Logo.png");
        StringAssert.Contains(manifestText, @"Assets\Wide310x150Logo.png");
        StringAssert.Contains(manifestText, @"Assets\SplashScreen.png");
        Assert.DoesNotContain("NoiseAsset", manifestText, StringComparison.Ordinal);
    }

    [TestMethod]
    public void BrandedWindowsAndAboutPage_UseTheApplicationLogo()
    {
        var files = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Windows", "SettingsWindow.xaml"),
            Path.Combine(AppContext.BaseDirectory, "Windows", "OnboardingWindow.xaml"),
            Path.Combine(AppContext.BaseDirectory, "Settings", "AboutSettingsPage.xaml")
        };

        foreach (var file in files)
        {
            StringAssert.Contains(
                XDocument.Load(file).ToString(),
                "ms-appx:///Assets/AppLogo.png",
                Path.GetFileName(file));
        }
    }

    [TestMethod]
    public void DiagnosticsPage_ExposesPrivacyAndLogManagementActions()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "DiagnosticsSettingsPage.xaml"));
        var text = document.ToString();

        StringAssert.Contains(text, "kişisel yol ve medya geçmişi kaydedilmez");
        StringAssert.Contains(text, "OnRefreshClick");
        StringAssert.Contains(text, "OnOpenFolderClick");
        StringAssert.Contains(text, "OnExportClick");
        StringAssert.Contains(text, "OnClearClick");
    }

    [TestMethod]
    public void AppearancePage_ExposesLivePreviewControls()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "AppearanceSettingsPage.xaml"));
        var text = document.ToString();

        StringAssert.Contains(text, "CollapsedWidth");
        StringAssert.Contains(text, "HoverWidth");
        StringAssert.Contains(text, "ExpandedWidth");
        StringAssert.Contains(text, "NotificationWidth");
        StringAssert.Contains(text, "BackgroundColor");
        StringAssert.Contains(text, "Opacity");
        StringAssert.Contains(text, "ShadowIntensity");
        StringAssert.Contains(text, "AnimationSpeed");
        StringAssert.Contains(text, "ResetAppearanceCommand");
    }

    [TestMethod]
    public void AppearancePage_UsesViewportWidthAndNamesInteractiveControls()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "AppearanceSettingsPage.xaml"));
        var scrollViewer = document.Descendants().Single(element =>
            element.Name.LocalName == "ScrollViewer");
        var rootStack = scrollViewer.Elements().Single(element =>
            element.Name.LocalName == "StackPanel");
        var interactiveControls = document.Descendants().Where(element =>
            element.Name.LocalName is "ComboBox" or "NumberBox" or "Slider" or "TextBox").ToArray();

        Assert.AreEqual("Stretch", scrollViewer.Attribute("HorizontalContentAlignment")?.Value);
        Assert.AreEqual("820", rootStack.Attribute("MaxWidth")?.Value);
        Assert.AreEqual("Center", rootStack.Attribute("HorizontalAlignment")?.Value);
        Assert.IsTrue(interactiveControls.All(control => control.Attributes().Any(attribute =>
            attribute.Name.LocalName == "AutomationProperties.Name")));

        var expandedHeight = document.Descendants().Single(element =>
            element.Name.LocalName == "NumberBox" &&
            element.Attribute("Value")?.Value == "{Binding ExpandedHeight, Mode=TwoWay}");
        Assert.AreEqual("260", expandedHeight.Attribute("Minimum")?.Value);
    }

    [TestMethod]
    public void SettingsAndOnboardingComboBoxes_UseStableVisibleSelections()
    {
        var directories = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Settings"),
            Path.Combine(AppContext.BaseDirectory, "Onboarding")
        };
        var comboBoxes = directories
            .SelectMany(directory => Directory.GetFiles(directory, "*.xaml"))
            .SelectMany(file => XDocument.Load(file).Descendants())
            .Where(element => element.Name.LocalName == "ComboBox")
            .ToArray();

        Assert.IsNotEmpty(comboBoxes);
        Assert.IsTrue(comboBoxes.All(comboBox => comboBox.Attribute("SelectedIndex") is not null));
        Assert.IsTrue(comboBoxes.All(comboBox =>
            comboBox.Attribute("ItemTemplate") is not null ||
            comboBox.Attribute("DisplayMemberPath") is not null));
        Assert.IsTrue(comboBoxes.All(comboBox => comboBox.Attribute("SelectedValuePath") is null));
        Assert.IsTrue(comboBoxes.All(comboBox => comboBox.Attributes().Any(attribute =>
            attribute.Name.LocalName == "AutomationProperties.Name")));
    }

    [TestMethod]
    public void PrimarySettingsPages_StretchWithoutAForcedDesktopWidth()
    {
        var pages = new[]
        {
            "GeneralSettingsPage.xaml",
            "MediaSettingsPage.xaml",
            "ModulesSettingsPage.xaml",
            "NotificationSettingsPage.xaml"
        };

        foreach (var page in pages)
        {
            var document = XDocument.Load(Path.Combine(
                AppContext.BaseDirectory,
                "Settings",
                page));
            var scrollViewer = document.Descendants().Single(element =>
                element.Name.LocalName == "ScrollViewer");
            var rootStack = scrollViewer.Elements().Single(element =>
                element.Name.LocalName == "StackPanel");

            Assert.IsNull(rootStack.Attribute("Width"), page);
            Assert.AreEqual("820", rootStack.Attribute("MaxWidth")?.Value, page);
            Assert.AreEqual("Stretch", rootStack.Attribute("HorizontalAlignment")?.Value, page);
            Assert.AreEqual(
                "Stretch",
                scrollViewer.Attribute("HorizontalContentAlignment")?.Value,
                page);
        }
    }

    [TestMethod]
    public void GeneralPage_OffersTurkishAndEnglishLanguageSelection()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "GeneralSettingsPage.xaml"));
        var text = document.ToString();

        StringAssert.Contains(text, "ItemsSource=\"{Binding Languages}\"");
        StringAssert.Contains(text, "SelectedIndex=\"{Binding LanguageIndex, Mode=TwoWay}\"");
        StringAssert.Contains(text, "PassiveModuleReturnSeconds");
        StringAssert.Contains(text, "Minimum=\"3\"");
        StringAssert.Contains(text, "Maximum=\"30\"");
    }

    [TestMethod]
    public void TimePage_ExposesTimerGuidanceAndGlobalHotKeys()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "TimeAndHotKeysSettingsPage.xaml"));
        var text = document.ToString();

        StringAssert.Contains(text, "Zamanlayıcı ve kronometre");
        StringAssert.Contains(text, "HotKeysEnabled");
        StringAssert.Contains(text, "ToggleDockHotKey");
        StringAssert.Contains(text, "TimerPauseResumeHotKey");
    }

    [TestMethod]
    public void ModulesPage_ExposesPerModuleSettingsAndOptInPrivacyControls()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "ModulesSettingsPage.xaml"));
        var text = document.ToString();

        StringAssert.Contains(text, "ModuleItems");
        StringAssert.Contains(text, "ShowSensitiveContentInFullscreen");
        StringAssert.Contains(text, "ShowSensitiveContentWhenLocked");
        StringAssert.Contains(text, "başlangıçta toplu izin istemez");
    }

    [TestMethod]
    public void TimerExpandedView_ContainsTimerAndStopwatchControls()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "TimerExpandedView.xaml"));
        var text = document.ToString();

        StringAssert.Contains(text, "Zamanlayıcı");
        StringAssert.Contains(text, "Kronometre");
        StringAssert.Contains(text, "StartPresetCommand");
        StringAssert.Contains(text, "AddLapCommand");
    }

    [TestMethod]
    public void TimerExpandedView_NamesCustomDurationFields()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "TimerExpandedView.xaml"));
        var numberBoxes = document.Descendants()
            .Where(element => element.Name.LocalName == "NumberBox")
            .ToArray();

        Assert.HasCount(3, numberBoxes);
        Assert.IsTrue(numberBoxes.All(control => control.Attributes().Any(attribute =>
            attribute.Name.LocalName == "AutomationProperties.Name")));
    }

    [TestMethod]
    public void TimerExpandedView_FitsDockAndExplainsAlarm()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "TimerExpandedView.xaml"));
        var tabItems = document.Descendants()
            .Where(element => element.Name.LocalName == "TabViewItem")
            .ToArray();
        var text = document.ToString();

        Assert.HasCount(2, tabItems);
        Assert.IsTrue(tabItems.All(item => item.Attribute("IsClosable")?.Value == "False"));
        StringAssert.Contains(text, "SelectedIndex=\"{Binding SelectedToolIndex, Mode=TwoWay}\"");
        StringAssert.Contains(text, "CommandParameter=\"1\"");
        StringAssert.Contains(text, "Süre dolduğunda alarm sesi çalar.");
        StringAssert.Contains(text, "AccentButtonStyle");
    }
}
