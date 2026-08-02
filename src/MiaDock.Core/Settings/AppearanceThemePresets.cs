using MiaDock.Core.Theming;

namespace MiaDock.Core.Settings;

public static class AppearanceThemePresets
{
    public static AppearanceSettings ApplyWhenSafe(AppearanceSettings current, ThemeStyle theme)
    {
        ArgumentNullException.ThrowIfNull(current);
        var next = current with { Theme = theme };
        if (!UsesBuiltInPreset(current))
        {
            return next;
        }

        return theme switch
        {
            ThemeStyle.AppleLike => next with
            {
                BackgroundColor = "#000000",
                AccentColor = "#FFFFFF",
                Opacity = 1,
                ShadowIntensity = 0
            },
            ThemeStyle.Windows11Mica => next with
            {
                BackgroundColor = "#202124",
                AccentColor = "#60CDFF",
                Opacity = 1,
                ShadowIntensity = 0.45
            },
            ThemeStyle.Windows11MicaAlt => next with
            {
                BackgroundColor = "#191B24",
                AccentColor = "#7AA2F7",
                Opacity = 1,
                ShadowIntensity = 0.55
            },
            ThemeStyle.Windows11Acrylic => next with
            {
                BackgroundColor = "#202124",
                AccentColor = "#60CDFF",
                Opacity = 0.96,
                ShadowIntensity = 0.45
            },
            ThemeStyle.Windows11AcrylicThin => next with
            {
                BackgroundColor = "#16181D",
                AccentColor = "#8AB4F8",
                Opacity = 0.92,
                ShadowIntensity = 0.35
            },
            ThemeStyle.BlurredGlass => next with
            {
                BackgroundColor = "#141414",
                AccentColor = "#FFFFFF",
                Opacity = 1,
                ShadowIntensity = 0.2
            },
            ThemeStyle.OledBlack => next with
            {
                BackgroundColor = "#000000",
                AccentColor = "#FFFFFF",
                Opacity = 1,
                ShadowIntensity = 0
            },
            ThemeStyle.NeutralFrostedGlass => next with
            {
                BackgroundColor = "#101010",
                AccentColor = "#FFFFFF",
                Opacity = 0.78,
                ShadowIntensity = 0.18
            },
            ThemeStyle.AdaptiveFluent => next with
            {
                BackgroundColor = "#202124",
                AccentColor = "#60CDFF",
                Opacity = 0.94,
                ShadowIntensity = 0.42
            },
            _ => next
        };
    }

    private static bool UsesBuiltInPreset(AppearanceSettings appearance) =>
        appearance.Theme switch
        {
            ThemeStyle.AppleLike =>
                PaletteMatches(appearance, "#000000", "#FFFFFF", 1, 0),
            ThemeStyle.Windows11Mica =>
                PaletteMatches(appearance, "#202124", "#60CDFF", 1, 0.45),
            ThemeStyle.Windows11MicaAlt =>
                PaletteMatches(appearance, "#191B24", "#7AA2F7", 1, 0.55),
            ThemeStyle.Windows11Acrylic =>
                PaletteMatches(appearance, "#202124", "#60CDFF", 0.96, 0.45),
            ThemeStyle.Windows11AcrylicThin =>
                PaletteMatches(appearance, "#16181D", "#8AB4F8", 0.92, 0.35),
            ThemeStyle.BlurredGlass =>
                PaletteMatches(appearance, "#141414", "#FFFFFF", 1, 0.2),
            ThemeStyle.OledBlack =>
                PaletteMatches(appearance, "#000000", "#FFFFFF", 1, 0),
            ThemeStyle.NeutralFrostedGlass =>
                PaletteMatches(appearance, "#101010", "#FFFFFF", 0.78, 0.18),
            ThemeStyle.AdaptiveFluent =>
                PaletteMatches(appearance, "#202124", "#60CDFF", 0.94, 0.42),
            _ => false
        };

    private static bool PaletteMatches(
        AppearanceSettings appearance,
        string background,
        string accent,
        double opacity,
        double shadow) =>
        string.Equals(appearance.BackgroundColor, background, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(appearance.AccentColor, accent, StringComparison.OrdinalIgnoreCase) &&
        Math.Abs(appearance.Opacity - opacity) < 0.001 &&
        Math.Abs(appearance.ShadowIntensity - shadow) < 0.001;
}
