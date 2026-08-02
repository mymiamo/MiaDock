using System.Xml.Linq;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class ThemeResourceTests
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [TestMethod]
    public void PrimitiveTokens_ContainRequiredIslandDimensions()
    {
        var keys = ReadResourceKeys("PrimitiveTokens.xaml");

        CollectionAssert.IsSubsetOf(
            new[]
            {
                "IslandCollapsedWidth",
                "IslandCollapsedHeight",
                "IslandExpandedWidth",
                "IslandExpandedHeight",
                "IslandCornerRadius"
            },
            keys.ToArray());
    }

    [TestMethod]
    public void PrimitiveTokens_UseExpandedDashboardBaseline()
    {
        var document = LoadTheme("PrimitiveTokens.xaml");
        var values = document.Root!.Elements()
            .Where(element => element.Attribute(XamlNamespace + "Key") is not null)
            .ToDictionary(
                element => element.Attribute(XamlNamespace + "Key")!.Value,
                element => element.Value,
                StringComparer.Ordinal);

        Assert.AreEqual("548", values["IslandExpandedWidth"]);
        Assert.AreEqual("360", values["IslandExpandedHeight"]);
    }

    [TestMethod]
    public void SemanticTokens_DefineDefaultLightAndHighContrastThemes()
    {
        var document = LoadTheme("SemanticTokens.xaml");
        var themeKeys = document.Descendants()
            .Attributes(XamlNamespace + "Key")
            .Select(attribute => attribute.Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.IsTrue(themeKeys.Contains("Default"));
        Assert.IsTrue(themeKeys.Contains("Light"));
        Assert.IsTrue(themeKeys.Contains("HighContrast"));
    }

    [TestMethod]
    public void DockDesignTokens_DefineProfessionalSpacingSizingAndTypographyScale()
    {
        var keys = ReadResourceKeys("DockDesignTokens.xaml");

        CollectionAssert.IsSubsetOf(
            new[]
            {
                "DockSpace4",
                "DockSpace8",
                "DockSpace12",
                "DockSpace16",
                "DockSpace24",
                "DockTouchTargetSize",
                "DockIconSize",
                "DockFontSizeDisplay",
                "DockFontSizeTitle",
                "DockFontSizeBody",
                "DockFontSizeCaption",
                "DockRadiusSmall",
                "DockRadiusMedium",
                "DockRadiusLarge",
                "DockRadiusPill",
                "DockCompactContentPadding",
                "DockHoverContentPadding",
                "DockExpandedContentPadding"
            },
            keys.ToArray());
    }

    [TestMethod]
    public void DockDesignTokens_DefineAccessibleSemanticSurfacesForEverySystemTheme()
    {
        var document = LoadTheme("DockDesignTokens.xaml");
        var themeDictionaries = document.Descendants()
            .Where(element => element.Name.LocalName == "ResourceDictionary")
            .Where(element => element.Attribute(XamlNamespace + "Key") is not null)
            .ToDictionary(
                element => element.Attribute(XamlNamespace + "Key")!.Value,
                element => element,
                StringComparer.Ordinal);
        var requiredKeys = new[]
        {
            "DockSectionBrush",
            "DockSurfaceStrokeBrush",
            "DockDividerBrush",
            "DockFocusStrokeBrush",
            "DockStatusNeutralFillBrush",
            "DockStatusPositiveFillBrush",
            "DockStatusWarningFillBrush",
            "DockStatusCriticalFillBrush"
        };

        foreach (var theme in new[] { "Default", "Light", "HighContrast" })
        {
            Assert.IsTrue(themeDictionaries.ContainsKey(theme));
            var keys = themeDictionaries[theme].Elements()
                .Select(element => element.Attribute(XamlNamespace + "Key")?.Value)
                .Where(key => key is not null)
                .ToHashSet(StringComparer.Ordinal);
            CollectionAssert.IsSubsetOf(requiredKeys, keys.ToArray());
        }
    }

    [TestMethod]
    public void DockControlStyles_DefineSharedProfessionalComponents()
    {
        var document = LoadTheme("DockControlStyles.xaml");
        var styles = document.Root!.Elements()
            .Where(element => element.Name.LocalName == "Style")
            .ToDictionary(
                element => element.Attribute(XamlNamespace + "Key")!.Value,
                element => element,
                StringComparer.Ordinal);

        CollectionAssert.IsSubsetOf(
            new[]
            {
                "DockSurfaceStyle",
                "DockSectionStyle",
                "DockIconButtonStyle",
                "DockInlineButtonStyle",
                "DockSegmentToggleButtonStyle",
                "DockCompactModuleLayoutStyle",
                "DockExpandedModuleLayoutStyle",
                "DockNotificationLayoutStyle",
                "DockModuleIconStyle",
                "DockModuleIconContainerStyle",
                "DockNotificationIconContainerStyle",
                "DockProgressBarStyle",
                "DockStatusBadgeStyle",
                "DockModuleHeaderStyle",
                "DockDividerStyle",
                "DockDisplayTextStyle",
                "DockMetricTextStyle",
                "DockTitleTextStyle",
                "DockBodyTextStyle",
                "DockCaptionTextStyle",
                "DockModuleHeaderTextStyle",
                "DockStatusTextStyle"
            },
            styles.Keys.ToArray());

        var iconButtonText = styles["DockIconButtonStyle"].ToString();
        StringAssert.Contains(iconButtonText, "Property=\"MinWidth\" Value=\"44\"");
        StringAssert.Contains(iconButtonText, "Property=\"MinHeight\" Value=\"44\"");
        StringAssert.Contains(iconButtonText, "UseSystemFocusVisuals");

        var statusBadgeText = styles["DockStatusBadgeStyle"].ToString();
        StringAssert.Contains(
            statusBadgeText,
            "Property=\"CornerRadius\" Value=\"5\"");
        Assert.IsFalse(statusBadgeText.Contains("Value=\"999\"", StringComparison.Ordinal));

        var segmentToggleText = styles["DockSegmentToggleButtonStyle"].ToString();
        StringAssert.Contains(segmentToggleText, "Property=\"CornerRadius\" Value=\"6\"");
        StringAssert.Contains(segmentToggleText, "UseSystemFocusVisuals");
    }

    [DataRow("AppleLikeTheme.xaml")]
    [DataRow("Windows11Theme.xaml")]
    [DataRow("BlurredGlassTheme.xaml")]
    [DataRow("OledBlackTheme.xaml")]
    [DataRow("NeutralFrostedGlassTheme.xaml")]
    [DataRow("AdaptiveFluentTheme.xaml")]
    [TestMethod]
    public void StyleTheme_ProvidesRequiredSemanticOverrides(string fileName)
    {
        var keys = ReadResourceKeys(fileName);

        Assert.IsTrue(keys.Contains("IslandStyleSurfaceBrush"));
        Assert.IsTrue(keys.Contains("IslandStyleControlBrush"));
        Assert.IsTrue(keys.Contains("IslandStyleAccentBrush"));
        Assert.IsTrue(keys.Contains("IslandAccentForegroundBrush"));
        Assert.IsTrue(keys.Contains("IslandIconButtonRestBrush"));
        Assert.IsTrue(keys.Contains("IslandIconButtonPointerOverBrush"));
        Assert.IsTrue(keys.Contains("IslandIconButtonPressedBrush"));
        Assert.IsTrue(keys.Contains("IslandIconButtonForegroundBrush"));
        Assert.IsTrue(keys.Contains("IslandIconButtonCornerRadius"));
    }

    [TestMethod]
    public void AppleTheme_UsesTransparentCircularIconButtons()
    {
        var document = LoadTheme("AppleLikeTheme.xaml");
        var resources = document.Root!.Elements()
            .Where(element => element.Attribute(XamlNamespace + "Key") is not null)
            .ToDictionary(
                element => element.Attribute(XamlNamespace + "Key")!.Value,
                element => element,
                StringComparer.Ordinal);

        Assert.AreEqual("Transparent",
            resources["IslandIconButtonRestBrush"].Attribute("Color")?.Value);
        Assert.AreEqual("#FFFFFFFF",
            resources["IslandIconButtonForegroundBrush"].Attribute("Color")?.Value);
        Assert.AreEqual("999",
            resources["IslandIconButtonCornerRadius"].Value.Trim());
        Assert.AreNotEqual("Transparent",
            resources["IslandIconButtonPointerOverBrush"].Attribute("Color")?.Value);
    }

    [TestMethod]
    public void ApplePalette_DefaultBlackSurface_MeetsTextAndIconContrast()
    {
        var background = Windows.UI.Color.FromArgb(255, 0, 0, 0);
        var palette = MiaDock.UI.Services.SolidThemeContrastPaletteFactory.Create(
            background,
            Windows.UI.Color.FromArgb(255, 255, 255, 255));

        Assert.IsGreaterThanOrEqualTo(
            7,
            MiaDock.UI.Services.SolidThemeContrastPaletteFactory.ContrastRatio(
                palette.Primary,
                background));
        Assert.IsGreaterThanOrEqualTo(
            4.5,
            MiaDock.UI.Services.SolidThemeContrastPaletteFactory.ContrastRatio(
                palette.Secondary,
                background));
        Assert.IsGreaterThanOrEqualTo(
            4.5,
            MiaDock.UI.Services.SolidThemeContrastPaletteFactory.ContrastRatio(
                palette.Accent,
                palette.Control));
        Assert.AreEqual(palette.Control.R, palette.Control.G);
        Assert.AreEqual(palette.Control.G, palette.Control.B);
        Assert.IsGreaterThan((byte)0, palette.Control.R);
        Assert.IsLessThan((byte)64, palette.Control.R);
    }

    [TestMethod]
    public void ApplePalette_LightCustomSurface_SwitchesToDarkForeground()
    {
        var background = Windows.UI.Color.FromArgb(255, 245, 245, 245);
        var palette = MiaDock.UI.Services.SolidThemeContrastPaletteFactory.Create(
            background,
            Windows.UI.Color.FromArgb(255, 255, 255, 255));

        Assert.IsLessThan((byte)32, palette.Primary.R);
        Assert.IsGreaterThanOrEqualTo(
            7,
            MiaDock.UI.Services.SolidThemeContrastPaletteFactory.ContrastRatio(
                palette.Primary,
                background));
        Assert.IsGreaterThanOrEqualTo(
            4.5,
            MiaDock.UI.Services.SolidThemeContrastPaletteFactory.ContrastRatio(
                palette.Accent,
                palette.Control));
    }

    [DataRow(52, 52, 52, 52, 52, 52)]
    [DataRow(119, 119, 119, 119, 119, 119)]
    [DataRow(196, 148, 110, 196, 148, 110)]
    [DataRow(238, 238, 238, 255, 255, 255)]
    [TestMethod]
    public void ApplePalette_CustomSurfacesKeepAllReadableContentAtNormalTextContrast(
        int red,
        int green,
        int blue,
        int accentRed,
        int accentGreen,
        int accentBlue)
    {
        var background = Windows.UI.Color.FromArgb(
            255,
            checked((byte)red),
            checked((byte)green),
            checked((byte)blue));
        var palette = MiaDock.UI.Services.SolidThemeContrastPaletteFactory.Create(
            background,
            Windows.UI.Color.FromArgb(
                255,
                checked((byte)accentRed),
                checked((byte)accentGreen),
                checked((byte)accentBlue)));

        Assert.IsGreaterThanOrEqualTo(
            4.5,
            MiaDock.UI.Services.SolidThemeContrastPaletteFactory.ContrastRatio(
                palette.Primary,
                background));
        Assert.IsGreaterThanOrEqualTo(
            4.5,
            MiaDock.UI.Services.SolidThemeContrastPaletteFactory.ContrastRatio(
                palette.Secondary,
                background));
        Assert.IsGreaterThanOrEqualTo(
            4.5,
            MiaDock.UI.Services.SolidThemeContrastPaletteFactory.ContrastRatio(
                palette.Secondary,
                palette.Control));
        Assert.IsGreaterThanOrEqualTo(
            4.5,
            MiaDock.UI.Services.SolidThemeContrastPaletteFactory.ContrastRatio(
                palette.Accent,
                background));
        Assert.IsGreaterThanOrEqualTo(
            4.5,
            MiaDock.UI.Services.SolidThemeContrastPaletteFactory.ContrastRatio(
                palette.Accent,
                palette.Control));
        Assert.IsGreaterThanOrEqualTo(
            4.5,
            MiaDock.UI.Services.SolidThemeContrastPaletteFactory.ContrastRatio(
                palette.AccentForeground,
                palette.Accent));
    }

    [TestMethod]
    public void AccentFilledNotificationUsesAdaptiveForegroundInsteadOfHardCodedBlack()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "NotificationModuleNotificationView.xaml"));
        var icon = document.Descendants()
            .Single(element => element.Name.LocalName == "FontIcon");
        var iconContainer = document.Descendants()
            .Single(element => element.Name.LocalName == "Border");

        Assert.AreEqual(
            "{StaticResource DockModuleIconStyle}",
            icon.Attribute("Style")?.Value);
        Assert.AreEqual(
            "{StaticResource DockNotificationIconContainerStyle}",
            iconContainer.Attribute("Style")?.Value);
    }

    [TestMethod]
    public void BlurredGlassTheme_UsesTransparentNeutralOverlayAndCircularButtons()
    {
        var document = LoadTheme("BlurredGlassTheme.xaml");
        var resources = document.Root!.Elements()
            .Where(element => element.Attribute(XamlNamespace + "Key") is not null)
            .ToDictionary(
                element => element.Attribute(XamlNamespace + "Key")!.Value,
                element => element,
                StringComparer.Ordinal);

        var surface = resources["IslandStyleSurfaceBrush"];
        Assert.AreEqual("SolidColorBrush", surface.Name.LocalName);
        Assert.AreEqual("#14141414", surface.Attribute("Color")?.Value);
        Assert.AreEqual("Transparent",
            resources["IslandIconButtonRestBrush"].Attribute("Color")?.Value);
        Assert.AreEqual("999",
            resources["IslandIconButtonCornerRadius"].Value.Trim());
    }

    [TestMethod]
    public void OledTheme_UsesPureBlackSurfaceWithoutRestingButtonChrome()
    {
        var document = LoadTheme("OledBlackTheme.xaml");
        var resources = document.Root!.Elements()
            .Where(element => element.Attribute(XamlNamespace + "Key") is not null)
            .ToDictionary(
                element => element.Attribute(XamlNamespace + "Key")!.Value,
                element => element,
                StringComparer.Ordinal);

        Assert.AreEqual("#FF000000", resources["IslandStyleSurfaceBrush"].Attribute("Color")?.Value);
        Assert.AreEqual("Transparent", resources["IslandIconButtonRestBrush"].Attribute("Color")?.Value);
    }

    [TestMethod]
    public void AdaptiveFluentTheme_DefinesDarkLightAndHighContrastResources()
    {
        var document = LoadTheme("AdaptiveFluentTheme.xaml");
        var themeKeys = document.Descendants()
            .Attributes(XamlNamespace + "Key")
            .Select(attribute => attribute.Value)
            .ToHashSet(StringComparer.Ordinal);
        var text = document.ToString();

        CollectionAssert.IsSubsetOf(new[] { "Default", "Light", "HighContrast" }, themeKeys.ToArray());
        StringAssert.Contains(text, "SystemAccentColor");
        StringAssert.Contains(text, "SystemColorWindowColor");
    }

    [TestMethod]
    public void IconButtonStyle_ChangesSurfaceOnlyForInteractionStates()
    {
        var document = LoadTheme("ControlStyles.xaml");
        var style = document.Descendants().Single(element =>
            element.Attribute(XamlNamespace + "Key")?.Value == "IslandIconButtonStyle");
        var stateNames = style.Descendants()
            .Where(element => element.Name.LocalName == "VisualState")
            .Select(element => element.Attribute(XamlNamespace + "Name")?.Value)
            .ToArray();
        var text = style.ToString();

        CollectionAssert.IsSubsetOf(
            new[] { "Normal", "PointerOver", "Pressed", "Disabled" },
            stateNames);
        StringAssert.Contains(text, "IslandIconButtonPointerOverBrush");
        StringAssert.Contains(text, "IslandIconButtonPressedBrush");
    }

    [TestMethod]
    public void MotionTokens_DefineEveryPhaseFourDuration()
    {
        var keys = ReadResourceKeys("MotionTokens.xaml");

        CollectionAssert.IsSubsetOf(
            new[]
            {
                "IslandHoverTransitionMilliseconds",
                "IslandExpandTransitionMilliseconds",
                "IslandCollapseTransitionMilliseconds",
                "IslandNotificationEnterMilliseconds",
                "IslandNotificationExitMilliseconds",
                "IslandContentRefreshMilliseconds",
                "IslandPointerExitDelayMilliseconds",
                "IslandNotificationDurationMilliseconds",
                "IslandExpandedInactivityMilliseconds"
            },
            keys.ToArray());
    }

    private static HashSet<string> ReadResourceKeys(string fileName) => LoadTheme(fileName)
        .Descendants()
        .Attributes(XamlNamespace + "Key")
        .Select(attribute => attribute.Value)
        .ToHashSet(StringComparer.Ordinal);

    private static XDocument LoadTheme(string fileName) => XDocument.Load(
        Path.Combine(AppContext.BaseDirectory, "Themes", fileName));
}
