using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class SettingsWindowTests
{
    [TestMethod]
    public void DockControlStaticText_HasEnglishLocalizationEntry()
    {
        var localizationSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Localization",
            "AppLocalizationService.cs"));
        var missing = Directory.GetFiles(
                Path.Combine(AppContext.BaseDirectory, "Controls"),
                "*.xaml")
            .SelectMany(file => Regex.Matches(
                    File.ReadAllText(file),
                    "(?:Text|Content|Header|PlaceholderText|ToolTipService\\.ToolTip|AutomationProperties\\.Name|AutomationProperties\\.HelpText)=\"([^\"]*[A-Za-zÇĞİÖŞÜçğıöşü][^\"]*)\"")
                .Select(match => match.Groups[1].Value))
            .Where(value => !value.StartsWith("{Binding", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .Where(value => !Regex.IsMatch(
                localizationSource,
                $"(?m)^\\s*\\[\"{Regex.Escape(value)}\"\\]\\s*=\\s*\""))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.HasCount(0, missing, $"Missing English entries: {string.Join(", ", missing)}");
    }

    [TestMethod]
    public void FirstSetup_StartsWithPersistentBilingualLanguageChoice()
    {
        var welcome = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Onboarding",
            "WelcomeStepView.xaml"));
        var text = welcome.ToString();
        var firstContent = welcome.Root!
            .Elements()
            .Single()
            .Elements()
            .First();
        var viewModelSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "ViewModels",
            "OnboardingViewModel.cs"));

        Assert.AreEqual("Border", firstContent.Name.LocalName);
        StringAssert.Contains(text, "Dil / Language");
        StringAssert.Contains(text, "ItemsSource=\"{Binding Languages}\"");
        StringAssert.Contains(text, "SelectedIndex=\"{Binding LanguageIndex, Mode=TwoWay}\"");
        StringAssert.Contains(viewModelSource, "General = settings.General with { Language = value }");
        StringAssert.Contains(viewModelSource, "FlushLanguagePreferenceAsync");
        StringAssert.Contains(viewModelSource, "_localization.SetLanguage(value)");
    }

    [TestMethod]
    public void OnboardingStaticText_HasEnglishLocalizationEntry()
    {
        var localizationSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Localization",
            "AppLocalizationService.cs"));
        var bilingualText = new HashSet<string>(StringComparer.Ordinal)
        {
            "Dil / Language",
            "Devam etmek istediğiniz dili seçin / Choose your language"
        };
        var missing = Directory.GetFiles(
                Path.Combine(AppContext.BaseDirectory, "Onboarding"),
                "*.xaml")
            .SelectMany(file => Regex.Matches(
                    File.ReadAllText(file),
                    "(?:Text|Content|Header|OnContent|OffContent|PlaceholderText|ToolTipService\\.ToolTip|AutomationProperties\\.Name|AutomationProperties\\.HelpText|Title|Message)=\"([^\"]*[A-Za-zÇĞİÖŞÜçğıöşü][^\"]*)\"")
                .Select(match => match.Groups[1].Value))
            .Where(value => !value.StartsWith("{Binding", StringComparison.Ordinal))
            .Where(value => !bilingualText.Contains(value))
            .Distinct(StringComparer.Ordinal)
            .Where(value => !Regex.IsMatch(
                localizationSource,
                $"(?m)^\\s*\\[\"{Regex.Escape(value)}\"\\]\\s*=\\s*\""))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.HasCount(0, missing, $"Missing onboarding English entries: {string.Join(", ", missing)}");
    }

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
            new[] { "home", "general", "focus", "modules", "appearance", "media", "time", "notifications", "fullscreen", "monitor", "tray", "startup", "diagnostics", "about" },
            tags);
    }

    [TestMethod]
    public void SettingsWindow_UsesResponsiveFluentNavigationAndSearch()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "SettingsWindow.xaml"));
        var text = document.ToString();

        StringAssert.Contains(text, "CompactPaneLength=\"60\"");
        StringAssert.Contains(text, "PaneDisplayMode=\"Left\"");
        StringAssert.Contains(text, "IsPaneOpen=\"True\"");
        StringAssert.Contains(text, "PlaceholderText=\"Ayarlarda ara\"");
        StringAssert.Contains(text, "SettingsWindowBackgroundBrush");
        Assert.DoesNotContain("#171B2B", text, StringComparison.Ordinal);
        Assert.DoesNotContain("#1B2032", text, StringComparison.Ordinal);
    }

    [TestMethod]
    public void SettingsSearch_IndexesTurkishAndEnglishAtTheSameTime()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "SettingsWindow.xaml.cs"));

        StringAssert.Contains(source, "TurkishTitle");
        StringAssert.Contains(source, "EnglishTitle");
        StringAssert.Contains(source, "TurkishDescription");
        StringAssert.Contains(source, "EnglishDescription");
        StringAssert.Contains(source, "Görünürlük, etkileşim, dil, saat ve dock konumu");
        StringAssert.Contains(source, "Visibility, interaction, language, clock and dock position");
        StringAssert.Contains(source, "radius");
        StringAssert.Contains(source, "usernotificationlistener");
        StringAssert.Contains(source, "Normalize(NormalizationForm.FormD)");
        StringAssert.Contains(source, ".OrderBy(item => item.Score(query))");
    }

    [TestMethod]
    public void LanguageChange_RefreshesNavigationItemsOutsideTheVisualTree()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "SettingsWindow.xaml.cs"));

        StringAssert.Contains(source, "ApplyNavigationLocalization()");
        StringAssert.Contains(source, "Navigation.MenuItems.OfType<NavigationViewItem>()");
        StringAssert.Contains(source, "item.Content = text");
        StringAssert.Contains(source, "ToolTipService.SetToolTip(item, text)");
    }

    [TestMethod]
    public void ModuleConsent_ExplainsEveryServiceInSettingsAndOnboarding()
    {
        var catalog = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Services",
            "ModuleServiceDisclosureCatalog.cs"));
        var dialog = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Dialogs",
            "ModuleServiceConsentDialog.cs"));
        var settings = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "ModulesSettingsPage.xaml.cs"));
        var onboarding = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "OnboardingWindow.xaml.cs"));

        foreach (var moduleId in new[]
                 {
                     "media", "volume", "system-activity", "battery",
                     "network", "bluetooth", "timer", "notifications",
                     "transfers"
                 })
        {
            StringAssert.Contains(catalog, $"\"{moduleId}\"");
        }

        StringAssert.Contains(catalog, "Global System Media Transport Controls");
        StringAssert.Contains(catalog, "IAudioEndpointVolume");
        StringAssert.Contains(catalog, "Windows IP Helper API");
        StringAssert.Contains(catalog, "Windows UserNotificationListener");
        StringAssert.Contains(dialog, "Service used:");
        StringAssert.Contains(settings, "ModuleServiceConsentDialog");
        StringAssert.Contains(settings, "!disclosure.RequiresWindowsPermission");
        StringAssert.Contains(onboarding, "OnboardingStep.Modules");
        StringAssert.Contains(onboarding, "_approvedModuleIds");
        StringAssert.Contains(onboarding, "!disclosure.RequiresWindowsPermission");
    }

    [TestMethod]
    public void HomePage_ProvidesResponsiveStatusAndQuickSettingsCards()
    {
        var text = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "HomeSettingsPage.xaml")).ToString();

        StringAssert.Contains(text, "AdaptiveTrigger MinWindowWidth=\"760\"");
        StringAssert.Contains(text, "Hızlı dock ayarları");
        StringAssert.Contains(text, "StartupStatusMessage");
        StringAssert.Contains(text, "EnabledModuleSummary");
        StringAssert.Contains(text, "Microsoft Store güncellemeleri");
        StringAssert.Contains(text, "CheckForUpdatesCommand");
        StringAssert.Contains(text, "OpenStoreCommand");
        StringAssert.Contains(text, "SettingsCardBorderStyle");
    }

    [TestMethod]
    public void SettingsStyles_FollowSystemThemeAndHighContrast()
    {
        var text = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Themes",
            "SettingsStyles.xaml")).ToString();

        StringAssert.Contains(text, "ThemeDictionaries");
        StringAssert.Contains(text, "HighContrast");
        StringAssert.Contains(text, "SystemColorWindowColor");
        StringAssert.Contains(text, "CardBackgroundFillColorDefaultBrush");
        StringAssert.Contains(text, "SettingsCardBorderStyle");
    }

    [TestMethod]
    public void SettingsStaticText_HasEnglishLocalizationEntry()
    {
        var localizationSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Localization",
            "AppLocalizationService.cs"));
        var invariantText = new HashSet<string>(StringComparer.Ordinal)
        {
            "#FFFFFF",
            "MiaDock",
            "v"
        };
        var missing = Directory.GetFiles(
                Path.Combine(AppContext.BaseDirectory, "Settings"),
                "*.xaml")
            .SelectMany(file => Regex.Matches(
                    File.ReadAllText(file),
                    "(?:Text|Content|Header|OnContent|OffContent|PlaceholderText|ToolTipService\\.ToolTip|AutomationProperties\\.Name|AutomationProperties\\.HelpText|Title|Message)=\"([^\"]*[A-Za-zÇĞİÖŞÜçğıöşü][^\"]*)\"")
                .Select(match => match.Groups[1].Value))
            .Where(value => !value.StartsWith("{Binding", StringComparison.Ordinal))
            .Where(value => !invariantText.Contains(value))
            .Distinct(StringComparer.Ordinal)
            .Where(value => !Regex.IsMatch(
                localizationSource,
                $"(?m)^\\s*\\[\"{Regex.Escape(value)}\"\\]\\s*=\\s*\""))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.HasCount(0, missing, $"Missing Settings English entries: {string.Join(", ", missing)}");
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
        StringAssert.Contains(text, "Dock boyutları");
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
        Assert.AreEqual("1.2.1.0", identity?.Attribute("Version")?.Value);
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
    public void ApplicationManifest_MatchesThePackageVersion()
    {
        var package = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Package.appxmanifest"));
        var application = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "app.manifest"));
        var packageNamespace = package.Root!.GetDefaultNamespace();
        var packageVersion = package.Root
            .Element(packageNamespace + "Identity")
            ?.Attribute("Version")
            ?.Value;
        var applicationVersion = application.Root!
            .Elements()
            .Single(element => element.Name.LocalName == "assemblyIdentity")
            .Attribute("version")
            ?.Value;

        Assert.AreEqual("1.2.1.0", packageVersion);
        Assert.AreEqual(packageVersion, applicationVersion);
    }

    [TestMethod]
    public void FocusPage_ExplainsScopeAndCanOpenWindowsFocusSettings()
    {
        var text = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "FocusSettingsPage.xaml")).ToString();

        StringAssert.Contains(text, "Windows Rahatsız Etme ayarını değiştirmez");
        StringAssert.Contains(text, "OpenWindowsFocusSettingsCommand");
        StringAssert.Contains(text, "Windows Odak ayarlarını aç");
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
        StringAssert.Contains(text, "MotionPresets");
        StringAssert.Contains(text, "MotionIntensity");
        StringAssert.Contains(text, "MotionSpringiness");
        StringAssert.Contains(text, "MotionContentDelayMilliseconds");
        StringAssert.Contains(text, "MotionParallax");
        StringAssert.Contains(text, "MotionTransientBlur");
        StringAssert.Contains(text, "PreviewDock");
        StringAssert.Contains(text, "CompactPreviewContent");
        StringAssert.Contains(text, "HoverPreviewContent");
        StringAssert.Contains(text, "ExpandedPreviewContent");
        StringAssert.Contains(text, "OnTestAnimationClick");
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
        Assert.AreEqual(
            "{StaticResource SettingsPageRootStackPanelStyle}",
            rootStack.Attribute("Style")?.Value);
        Assert.IsTrue(interactiveControls.All(control => control.Attributes().Any(attribute =>
            attribute.Name.LocalName == "AutomationProperties.Name")));

        var expandedHeight = document.Descendants().Single(element =>
            element.Name.LocalName == "NumberBox" &&
            element.Attribute("Value")?.Value == "{Binding ExpandedHeight, Mode=TwoWay}");
        Assert.AreEqual("360", expandedHeight.Attribute("Minimum")?.Value);
        var expandedWidth = document.Descendants().Single(element =>
            element.Name.LocalName == "NumberBox" &&
            element.Attribute("Value")?.Value == "{Binding ExpandedWidth, Mode=TwoWay}");
        Assert.AreEqual("548", expandedWidth.Attribute("Minimum")?.Value);
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
            Assert.AreEqual(
                "{StaticResource SettingsPageRootStackPanelStyle}",
                rootStack.Attribute("Style")?.Value,
                page);
            Assert.AreEqual(
                "Stretch",
                scrollViewer.Attribute("HorizontalContentAlignment")?.Value,
                page);
        }

        var general = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "GeneralSettingsPage.xaml"));
        var generalStack = general.Descendants()
            .First(element => element.Name.LocalName == "ScrollViewer")
            .Elements()
            .Single(element => element.Name.LocalName == "StackPanel");
        Assert.AreEqual(
            "{StaticResource SettingsPageRootStackPanelStyle}",
            generalStack.Attribute("Style")?.Value);
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
        StringAssert.Contains(text, "AutomaticUpdateChecksEnabled");
        StringAssert.Contains(text, "Otomatik güncelleme denetimi");
        StringAssert.Contains(text, "ClockHourFormats");
        StringAssert.Contains(text, "ClockHourFormatIndex");
        StringAssert.Contains(text, "ShowClockSeconds");
        StringAssert.Contains(text, "ShowClockDate");
        StringAssert.Contains(text, "ClockDateFormats");
        StringAssert.Contains(text, "ShowClockWeekday");
    }

    [TestMethod]
    public void AboutPage_ExposesStoreUpdateStatusAndActions()
    {
        var text = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "AboutSettingsPage.xaml")).ToString();

        StringAssert.Contains(text, "StoreUpdateStatusMessage");
        StringAssert.Contains(text, "StoreUpdateVersionText");
        StringAssert.Contains(text, "CheckForUpdatesCommand");
        StringAssert.Contains(text, "OpenStoreCommand");
    }

    [TestMethod]
    public void StoreUpdateNotification_OpensTheMicrosoftStore()
    {
        var text = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "StoreUpdateNotificationView.xaml")).ToString();

        StringAssert.Contains(text, "Microsoft Store'da aç");
        StringAssert.Contains(text, "OnOpenStoreClick");
        StringAssert.Contains(text, "AutomationProperties.LiveSetting=\"Polite\"");
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
        StringAssert.Contains(text, "kullanılan Windows API'si veya yerel servis");
        StringAssert.Contains(text, "onayınız istenir");
        StringAssert.Contains(text, "Servisleri ve izinleri görüntüle");
        StringAssert.Contains(text, "OnShowServicesClick");
    }

    [TestMethod]
    public void FocusPage_ExposesProfileCrudAndActiveState()
    {
        var text = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "FocusSettingsPage.xaml")).ToString();

        StringAssert.Contains(text, "Profiles");
        StringAssert.Contains(text, "CanCreateProfile");
        StringAssert.Contains(text, "OnAddProfileClick");
        StringAssert.Contains(text, "OnEditProfileClick");
        StringAssert.Contains(text, "OnDeleteProfileClick");
        StringAssert.Contains(text, "OnResetProfileClick");
        StringAssert.Contains(text, "IsActive");
    }

    [TestMethod]
    public void FocusEditor_ProvidesValidatedBehaviorAndPrivacyFields()
    {
        var text = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Dialogs",
            "FocusProfileEditorDialog.xaml")).ToString();

        StringAssert.Contains(text, "ColorPicker");
        StringAssert.Contains(text, "DefaultDurationMinutes");
        StringAssert.Contains(text, "DockVisibilityOptions");
        StringAssert.Contains(text, "PriorityOptions");
        StringAssert.Contains(text, "AllowAllModules");
        StringAssert.Contains(text, "AllowFullscreenNotifications");
        StringAssert.Contains(text, "AllowSensitiveContentInFullscreen");
        StringAssert.Contains(text, "AllowSensitiveContentWhenLocked");
        StringAssert.Contains(text, "Schedules");
        StringAssert.Contains(text, "AutomationRules");
        StringAssert.Contains(text, "OnAddScheduleClick");
        StringAssert.Contains(text, "OnRemoveScheduleClick");
        StringAssert.Contains(text, "OnAddAutomationRuleClick");
        StringAssert.Contains(text, "OnRemoveAutomationRuleClick");
        StringAssert.Contains(text, "TimePicker");
        StringAssert.Contains(text, "HasError");
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
    public void TimerExpandedView_UsesAccessibleSegmentsAndIndependentPanels()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "TimerExpandedView.xaml"));
        var segments = document.Descendants()
            .Where(element => element.Name.LocalName == "ToggleButton")
            .ToArray();
        var text = document.ToString();

        Assert.HasCount(2, segments);
        Assert.IsFalse(document.Descendants().Any(element => element.Name.LocalName == "TabView"));
        StringAssert.Contains(text, "TimerPanel");
        StringAssert.Contains(text, "StopwatchPanel");
        StringAssert.Contains(text, "CommandParameter=\"5\"");
        StringAssert.Contains(text, "TimerSecondaryText");
        StringAssert.Contains(text, "AccentButtonStyle");
    }
}
