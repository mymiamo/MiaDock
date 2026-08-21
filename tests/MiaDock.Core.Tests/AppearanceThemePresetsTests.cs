using MiaDock.Core.Settings;
using MiaDock.Core.Theming;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class AppearanceThemePresetsTests
{
    [TestMethod]
    public void SwitchingFromDefaultApple_AppliesMicaPreset()
    {
        var result = AppearanceThemePresets.ApplyWhenSafe(
            AppearanceSettings.Default,
            ThemeStyle.Windows11Mica);

        Assert.AreEqual(ThemeStyle.Windows11Mica, result.Theme);
        Assert.AreEqual("#202124", result.BackgroundColor);
        Assert.AreEqual("#60CDFF", result.AccentColor);
        Assert.AreEqual(1, result.Opacity, 0.001);
        Assert.AreEqual(0.45, result.ShadowIntensity, 0.001);
    }

    [TestMethod]
    public void SwitchingTheme_PreservesCustomPalette()
    {
        var custom = AppearanceSettings.Default with
        {
            BackgroundColor = "#123456",
            AccentColor = "#ABCDEF",
            Opacity = 0.63,
            ShadowIntensity = 0.2
        };

        var result = AppearanceThemePresets.ApplyWhenSafe(custom, ThemeStyle.Windows11Acrylic);

        Assert.AreEqual(ThemeStyle.Windows11Acrylic, result.Theme);
        Assert.AreEqual("#123456", result.BackgroundColor);
        Assert.AreEqual("#ABCDEF", result.AccentColor);
        Assert.AreEqual(0.63, result.Opacity, 0.001);
        Assert.AreEqual(0.2, result.ShadowIntensity, 0.001);
    }

    [TestMethod]
    public void SwitchingBackFromWindowsPreset_RestoresApplePalette()
    {
        var windows = AppearanceThemePresets.ApplyWhenSafe(
            AppearanceSettings.Default,
            ThemeStyle.Windows11Mica);

        var result = AppearanceThemePresets.ApplyWhenSafe(windows, ThemeStyle.AppleLike);

        Assert.AreEqual(AppearanceSettings.Default, result);
    }

    [TestMethod]
    [DataRow(ThemeStyle.Windows11MicaAlt, "#191B24", "#7AA2F7", 1, 0.55)]
    [DataRow(ThemeStyle.Windows11Acrylic, "#202124", "#60CDFF", 0.96, 0.45)]
    [DataRow(ThemeStyle.Windows11AcrylicThin, "#16181D", "#8AB4F8", 0.92, 0.35)]
    [DataRow(ThemeStyle.BlurredGlass, "#141414", "#FFFFFF", 1, 0.2)]
    public void BuiltInWindowsTheme_AppliesItsPalette(
        ThemeStyle theme,
        string background,
        string accent,
        double opacity,
        double shadow)
    {
        var result = AppearanceThemePresets.ApplyWhenSafe(AppearanceSettings.Default, theme);

        Assert.AreEqual(theme, result.Theme);
        Assert.AreEqual(background, result.BackgroundColor);
        Assert.AreEqual(accent, result.AccentColor);
        Assert.AreEqual(opacity, result.Opacity, 0.001);
        Assert.AreEqual(shadow, result.ShadowIntensity, 0.001);
    }

    [TestMethod]
    public void CustomSolidColor_PreservesCurrentPalette()
    {
        var result = AppearanceThemePresets.ApplyWhenSafe(
            AppearanceSettings.Default,
            ThemeStyle.CustomSolidColor);

        Assert.AreEqual(ThemeStyle.CustomSolidColor, result.Theme);
        Assert.AreEqual(AppearanceSettings.Default.BackgroundColor, result.BackgroundColor);
        Assert.AreEqual(AppearanceSettings.Default.AccentColor, result.AccentColor);
        Assert.AreEqual(AppearanceSettings.Default.Opacity, result.Opacity);
        Assert.AreEqual(AppearanceSettings.Default.ShadowIntensity, result.ShadowIntensity);
    }

    [TestMethod]
    public void TozPembe_AppliesDustyPinkPaletteEvenFromCustomColors()
    {
        var custom = AppearanceSettings.Default with
        {
            BackgroundColor = "#123456",
            AccentColor = "#ABCDEF",
            Opacity = 0.63,
            ShadowIntensity = 0.8
        };

        var result = AppearanceThemePresets.ApplyWhenSafe(custom, ThemeStyle.TozPembe);

        Assert.AreEqual(ThemeStyle.TozPembe, result.Theme);
        Assert.AreEqual("#E4A0B0", result.BackgroundColor);
        Assert.AreEqual("#4A2430", result.AccentColor);
        Assert.AreEqual(1, result.Opacity, 0.001);
        Assert.AreEqual(0.2, result.ShadowIntensity, 0.001);
    }

    [TestMethod]
    public void SwitchingFromTozPembeToApple_RestoresTheBlackDefaultPalette()
    {
        var dustyPink = AppearanceThemePresets.ApplyWhenSafe(
            AppearanceSettings.Default,
            ThemeStyle.TozPembe);

        var result = AppearanceThemePresets.ApplyWhenSafe(dustyPink, ThemeStyle.AppleLike);

        Assert.AreEqual(AppearanceSettings.Default, result);
    }
}
