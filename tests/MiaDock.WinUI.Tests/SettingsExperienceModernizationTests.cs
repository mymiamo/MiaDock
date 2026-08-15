using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class SettingsExperienceModernizationTests
{
    [TestMethod]
    public void SettingsPages_AreWellFormedAndUseSharedSettingsPageStyles()
    {
        var settingsDirectory = Path.Combine(AppContext.BaseDirectory, "Settings");
        var pages = Directory.GetFiles(settingsDirectory, "*.xaml");

        Assert.IsNotEmpty(pages);
        foreach (var page in pages)
        {
            var document = XDocument.Load(page);
            var text = document.ToString();
            Assert.IsTrue(
                text.Contains("SettingsPageRootStackPanelStyle", StringComparison.Ordinal) ||
                text.Contains("SettingsPagePadding", StringComparison.Ordinal),
                Path.GetFileName(page));
        }
    }

    [TestMethod]
    public void SettingsThemeResources_FollowSystemThemeWithMicaAndHighContrast()
    {
        var styles = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Themes",
            "SettingsStyles.xaml")).ToString();
        var windowSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "SettingsWindow.xaml.cs"));

        StringAssert.Contains(styles, "ThemeDictionaries");
        StringAssert.Contains(styles, "x:Key=\"Default\"");
        StringAssert.Contains(styles, "x:Key=\"Light\"");
        StringAssert.Contains(styles, "x:Key=\"HighContrast\"");
        StringAssert.Contains(windowSource, "MicaBackdrop");
        StringAssert.Contains(windowSource, "Root.RequestedTheme = ElementTheme.Default");
        Assert.DoesNotContain("ThemeStyle.OledBlack", windowSource, StringComparison.Ordinal);
        StringAssert.Contains(windowSource, "AccessibilitySettings");
    }

    [TestMethod]
    public void SettingsWindow_ProvidesKeyboardSearchAndResponsiveNavigation()
    {
        var xaml = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "SettingsWindow.xaml")).ToString();
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "SettingsWindow.xaml.cs"));

        StringAssert.Contains(xaml, "KeyboardAccelerator Key=\"F\" Modifiers=\"Control\"");
        StringAssert.Contains(xaml, "KeyboardAcceleratorPlacementMode=\"Hidden\"");
        StringAssert.Contains(xaml, "AutomationProperties.Name=\"Ayarlar gezinmesi\"");
        StringAssert.Contains(source, "args.NewSize.Width < 720");
        StringAssert.Contains(source, "NavigationViewPaneDisplayMode.LeftMinimal");
        StringAssert.Contains(source, "SettingsSearch.Focus(FocusState.Keyboard)");
        StringAssert.Contains(xaml, "SubpageTabs");
        StringAssert.Contains(xaml, "SubpagePicker");
        StringAssert.Contains(xaml, "IsBackButtonVisible=\"Auto\"");
        StringAssert.Contains(xaml, "BackRequested=\"OnNavigationBackRequested\"");
        StringAssert.Contains(source, "TryGoBack()");
        StringAssert.Contains(source, "PushBackEntry");
        StringAssert.Contains(source, "OnBackKeyboardAcceleratorInvoked");
    }

    [TestMethod]
    public void SettingsNavigation_MapsEightCategoriesToSeventeenSearchableSubpages()
    {
        var xaml = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory, "Windows", "SettingsWindow.xaml")).ToString();
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Windows", "SettingsWindow.xaml.cs"));

        Assert.HasCount(8, XDocument.Parse(xaml).Descendants()
            .Where(element => element.Name.LocalName == "NavigationViewItem"));
        Assert.HasCount(17, Regex.Matches(source, "Search\\(\\\"").Cast<Match>());
        StringAssert.Contains(source, "CategoryId");
        StringAssert.Contains(source, "SubpageId");
        StringAssert.Contains(source, "FocusTarget");
        StringAssert.Contains(source, "NavigationAnnouncement");
        StringAssert.Contains(source, "s_lastSubpageId");
        StringAssert.Contains(xaml, "FooterMenuItems");
        StringAssert.Contains(xaml, "Tag=\"whats-new\"");
    }

    [TestMethod]
    public void SettingsTransitions_AreShortCancelableAndRespectReducedMotion()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Windows", "SettingsWindow.xaml.cs"));

        StringAssert.Contains(source, "TimeSpan.FromMilliseconds(200)");
        StringAssert.Contains(source, "StopAnimation(\"Opacity\")");
        StringAssert.Contains(source, "SetIsTranslationEnabled(PageHost, true)");
        StringAssert.Contains(source, "StopAnimation(\"Translation\")");
        StringAssert.Contains(source, "StartAnimation(\"Translation\"");
        Assert.DoesNotContain("visual.StartAnimation(\"Offset\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("visual.Offset =", source, StringComparison.Ordinal);
        StringAssert.Contains(source, "new UISettings().AnimationsEnabled");
    }

    [TestMethod]
    public void SubpageTabs_DoNotUseACompetingScrollOwningList()
    {
        var xaml = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory, "Windows", "SettingsWindow.xaml")).ToString();
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Windows", "SettingsWindow.xaml.cs"));

        StringAssert.Contains(xaml, "SubpageTabPanel");
        Assert.DoesNotContain("SelectionChanged=\"OnSubpageSelectionChanged\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ScrollViewer.HorizontalScrollMode", xaml, StringComparison.Ordinal);
        StringAssert.Contains(source, "OnSubpageTabClick");
        StringAssert.Contains(source, "ToggleButton");
    }

    [TestMethod]
    public void EverySettingsCategory_HasAThemeAwareColor()
    {
        var xaml = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory, "Windows", "SettingsWindow.xaml")).ToString();
        var styles = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory, "Themes", "SettingsStyles.xaml")).ToString();

        foreach (var category in new[] { "Home", "Personalization", "Focus", "Modules", "Shortcuts", "System", "Support", "WhatsNew" })
        {
            StringAssert.Contains(xaml, $"SettingsCategory{category}Brush");
            StringAssert.Contains(styles, $"SettingsCategory{category}Brush");
        }
    }

    [TestMethod]
    public void SelectedSubpageTab_IsTransparentAndSettingsWindowHasMinimumSize()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Windows", "SettingsWindow.xaml.cs"));

        StringAssert.Contains(source, "ToggleButtonBackgroundChecked");
        StringAssert.Contains(source, "ToggleButtonBackgroundCheckedPointerOver");
        StringAssert.Contains(source, "Microsoft.UI.Colors.Transparent");
        StringAssert.Contains(source, "Foreground = GetSettingsBrush(\"TextFillColorPrimaryBrush\")");
        StringAssert.Contains(source, "MinimumWindowWidth = 972");
        StringAssert.Contains(source, "MinimumWindowHeight = 692");
        StringAssert.Contains(source, "WindowMinimumSizeMonitor");
    }

    [TestMethod]
    public void Onboarding_UsesFiveDraftBackedStepsAndDefersPermissions()
    {
        var window = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Windows", "OnboardingWindow.xaml.cs"));
        var viewModel = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "ViewModels", "OnboardingViewModel.cs"));
        var xaml = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory, "Windows", "OnboardingWindow.xaml")).ToString();

        foreach (var step in new[] { "Welcome", "Personalization", "Interaction", "FeaturesAndPrivacy", "Ready" })
        {
            StringAssert.Contains(window, $"OnboardingStep.{step}");
        }
        StringAssert.Contains(viewModel, "Language = Language");
        StringAssert.Contains(viewModel, "Onboarding = new OnboardingSettings(true");
        StringAssert.Contains(viewModel, "RestorePreviewTheme");
        Assert.DoesNotContain("FlushLanguagePreferenceAsync", viewModel, StringComparison.Ordinal);
        StringAssert.Contains(window, "!disclosure.RequiresWindowsPermission");
        StringAssert.Contains(window, "args.NewSize.Width < 720");
    }

    [TestMethod]
    public void SettingsSpecificBadge_DoesNotConsumeDockStatusStyles()
    {
        var badge = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "ModuleAvailabilityBadge.xaml")).ToString();

        StringAssert.Contains(badge, "SettingsStatusBadgeBorderStyle");
        StringAssert.Contains(badge, "SettingsStatusBadgeTextStyle");
        Assert.DoesNotContain("DockStatusBadgeStyle", badge, StringComparison.Ordinal);
        Assert.DoesNotContain("DockStatusTextStyle", badge, StringComparison.Ordinal);
    }

    [TestMethod]
    public void HotKeyRecorder_ReleasesTabForKeyboardNavigation()
    {
        var repositoryRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repositoryRoot is not null && !Directory.Exists(Path.Combine(repositoryRoot.FullName, "src")))
        {
            repositoryRoot = repositoryRoot.Parent;
        }

        Assert.IsNotNull(repositoryRoot);
        var source = File.ReadAllText(Path.Combine(
            repositoryRoot.FullName,
            "src",
            "MiaDock.App",
            "Controls",
            "HotKeyRecorderControl.xaml.cs"));

        var tabCheck = source.IndexOf("args.Key == VirtualKey.Tab", StringComparison.Ordinal);
        var handled = source.IndexOf("args.Handled = true", StringComparison.Ordinal);

        Assert.IsGreaterThanOrEqualTo(0, tabCheck);
        Assert.IsGreaterThan(tabCheck, handled);
        StringAssert.Contains(source, "StopRecording();");
    }

    [TestMethod]
    public void SettingsLayouts_IncludeNarrowWidthReflow()
    {
        var moduleCard = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "ModuleSettingsCard.xaml")).ToString();
        var focusPage = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "FocusSettingsPage.xaml")).ToString();
        var aboutPage = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "AboutSettingsPage.xaml")).ToString();

        StringAssert.Contains(moduleCard, "CompactModuleDetails");
        StringAssert.Contains(moduleCard, "WideModuleDetails");
        StringAssert.Contains(focusPage, "CompactHeader");
        StringAssert.Contains(focusPage, "WideHeader");
        StringAssert.Contains(aboutPage, "CompactUpdateCard");
        StringAssert.Contains(aboutPage, "WideUpdateCard");
    }

    [TestMethod]
    public void FocusSettings_ExposeAccessibleMasterToggleAndDisableDependentContent()
    {
        var focusPage = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "FocusSettingsPage.xaml")).ToString();
        var searchSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "SettingsWindow.xaml.cs"));

        StringAssert.Contains(focusPage, "IsOn=\"{Binding IsFocusEnabled, Mode=TwoWay}\"");
        StringAssert.Contains(focusPage, "AutomationProperties.Name=\"Odak özelliklerini etkinleştir\"");
        StringAssert.Contains(focusPage, "AutomationProperties.HelpText=");
        StringAssert.Contains(focusPage, "IsEnabled=\"{Binding AreFocusDetailsEnabled}\"");
        StringAssert.Contains(searchSource, "Etkinleştir, devre dışı bırak");
        StringAssert.Contains(searchSource, "Enable, disable");
    }
}
