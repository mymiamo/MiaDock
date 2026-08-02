using Windows.UI;

namespace MiaDock.UI.Services;

public sealed record SolidThemeContrastPalette(
    Color Primary,
    Color Secondary,
    Color Accent,
    Color AccentForeground,
    Color Control,
    Color Stroke);

public static class SolidThemeContrastPaletteFactory
{
    private const double NormalTextMinimumRatio = 4.5;
    private static readonly Color LightForeground = Color.FromArgb(255, 255, 255, 255);
    private static readonly Color DarkForeground = Color.FromArgb(255, 0, 0, 0);

    public static SolidThemeContrastPalette Create(Color background, Color requestedAccent)
    {
        var primary = ContrastRatio(LightForeground, background) >=
                      ContrastRatio(DarkForeground, background)
            ? LightForeground
            : DarkForeground;
        var control = Mix(background, primary, 0.14);
        if (ContrastRatio(primary, control) < NormalTextMinimumRatio)
        {
            var opposite = primary.R > 127 ? DarkForeground : LightForeground;
            control = Mix(background, opposite, 0.12);
        }
        var secondary = EnsureContrast(
            Mix(background, primary, 0.68),
            background,
            control,
            primary,
            NormalTextMinimumRatio);
        var accent = EnsureContrast(
            requestedAccent,
            background,
            control,
            primary,
            NormalTextMinimumRatio);
        var accentForeground = ContrastRatio(LightForeground, accent) >=
                               ContrastRatio(DarkForeground, accent)
            ? LightForeground
            : DarkForeground;
        var stroke = Mix(background, primary, 0.24);

        return new SolidThemeContrastPalette(
            primary,
            secondary,
            accent,
            accentForeground,
            control,
            stroke);
    }

    public static double ContrastRatio(Color first, Color second)
    {
        var light = Math.Max(RelativeLuminance(first), RelativeLuminance(second));
        var dark = Math.Min(RelativeLuminance(first), RelativeLuminance(second));
        return (light + 0.05) / (dark + 0.05);
    }

    private static Color EnsureContrast(
        Color accent,
        Color surface,
        Color control,
        Color target,
        double minimumRatio)
    {
        var candidate = Color.FromArgb(255, accent.R, accent.G, accent.B);
        for (var step = 0; step <= 32; step++)
        {
            if (ContrastRatio(candidate, surface) >= minimumRatio &&
                ContrastRatio(candidate, control) >= minimumRatio)
            {
                return candidate;
            }

            candidate = Mix(candidate, target, 0.12);
        }

        return target;
    }

    private static Color Mix(Color from, Color to, double amount)
    {
        var normalized = Math.Clamp(amount, 0, 1);
        return Color.FromArgb(
            255,
            MixChannel(from.R, to.R, normalized),
            MixChannel(from.G, to.G, normalized),
            MixChannel(from.B, to.B, normalized));
    }

    private static byte MixChannel(byte from, byte to, double amount) =>
        checked((byte)Math.Round(from + (to - from) * amount));

    private static double RelativeLuminance(Color color) =>
        0.2126 * Linearize(color.R) +
        0.7152 * Linearize(color.G) +
        0.0722 * Linearize(color.B);

    private static double Linearize(byte channel)
    {
        var normalized = channel / 255d;
        return normalized <= 0.04045
            ? normalized / 12.92
            : Math.Pow((normalized + 0.055) / 1.055, 2.4);
    }
}
