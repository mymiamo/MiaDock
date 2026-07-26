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

    [DataRow("AppleLikeTheme.xaml")]
    [DataRow("Windows11Theme.xaml")]
    [DataRow("BlurredGlassTheme.xaml")]
    [TestMethod]
    public void StyleTheme_ProvidesRequiredSemanticOverrides(string fileName)
    {
        var keys = ReadResourceKeys(fileName);

        Assert.IsTrue(keys.Contains("IslandStyleSurfaceBrush"));
        Assert.IsTrue(keys.Contains("IslandStyleControlBrush"));
        Assert.IsTrue(keys.Contains("IslandStyleAccentBrush"));
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
            3,
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
            3,
            MiaDock.UI.Services.SolidThemeContrastPaletteFactory.ContrastRatio(
                palette.Accent,
                palette.Control));
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
