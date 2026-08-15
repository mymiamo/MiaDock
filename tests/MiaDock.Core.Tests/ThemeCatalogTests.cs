using MiaDock.Core.Settings;
using MiaDock.Core.Theming;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class ThemeCatalogTests
{
    [TestMethod]
    public void Catalog_DescribesEveryThemeExactlyOnce()
    {
        var descriptors = ThemeCatalog.All;

        Assert.HasCount(Enum.GetValues<ThemeStyle>().Length, descriptors);
        Assert.HasCount(descriptors.Count, descriptors.Select(item => item.Style).Distinct());
        Assert.IsTrue(descriptors.All(item =>
            !string.IsNullOrWhiteSpace(item.DisplayNameKey) &&
            item.ResourceFileName.EndsWith("Theme.xaml", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void NewThemes_AdvertiseAccurateCapabilities()
    {
        var oled = ThemeCatalog.Get(ThemeStyle.OledBlack).Capabilities;
        var glass = ThemeCatalog.Get(ThemeStyle.NeutralFrostedGlass).Capabilities;
        var adaptive = ThemeCatalog.Get(ThemeStyle.AdaptiveFluent).Capabilities;
        var tozPembe = ThemeCatalog.Get(ThemeStyle.TozPembe).Capabilities;

        Assert.AreEqual(ThemeBackdropKind.None, oled.Backdrop);
        Assert.IsTrue(oled.PrefersDarkContent);
        Assert.AreEqual(ThemeBackdropKind.ColorlessAcrylic, glass.Backdrop);
        Assert.IsTrue(glass.UsesTransparentSurface);
        Assert.AreEqual(ThemeBackdropKind.Mica, adaptive.Backdrop);
        Assert.IsTrue(adaptive.FollowsSystemTheme);
        Assert.IsFalse(adaptive.SupportsBackgroundColor);
        Assert.IsFalse(adaptive.SupportsAccentColor);
        Assert.AreEqual(ThemeBackdropKind.None, tozPembe.Backdrop);
        Assert.IsFalse(tozPembe.PrefersDarkContent);
        Assert.AreEqual("TozPembeTheme.xaml", ThemeCatalog.Get(ThemeStyle.TozPembe).ResourceFileName);
    }

    [TestMethod]
    public void ThemePresets_SafelyApplyNewBuiltInPalettes()
    {
        var oled = AppearanceThemePresets.ApplyWhenSafe(
            AppearanceSettings.Default,
            ThemeStyle.OledBlack);
        var glass = AppearanceThemePresets.ApplyWhenSafe(oled, ThemeStyle.NeutralFrostedGlass);
        var adaptive = AppearanceThemePresets.ApplyWhenSafe(glass, ThemeStyle.AdaptiveFluent);

        Assert.AreEqual("#000000", oled.BackgroundColor);
        Assert.AreEqual(0, oled.ShadowIntensity);
        Assert.AreEqual("#101010", glass.BackgroundColor);
        Assert.AreEqual(0.78, glass.Opacity, 0.001);
        Assert.AreEqual("#60CDFF", adaptive.AccentColor);
        Assert.AreEqual(0.94, adaptive.Opacity, 0.001);
    }
}
