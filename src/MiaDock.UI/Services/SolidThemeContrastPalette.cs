using Windows.UI;

namespace MiaDock.UI.Services;

public sealed record SolidThemeContrastPalette(
    Color Primary,
    Color Secondary,
    Color Accent,
    Color Control,
    Color Stroke);

public static class SolidThemeContrastPaletteFactory
{
    private static readonly Color LightForeground = Color.FromArgb(255, 250, 250, 252);
    private static readonly Color DarkForeground = Color.FromArgb(255, 17, 17, 19);

    public static SolidThemeContrastPalette Create(Color background, Color requestedAccent)
    {
        var primary = ContrastRatio(LightForeground, background) >=
                      ContrastRatio(DarkForeground, background)
            ? LightForeground
            : DarkForeground;
        var secondary = Mix(background, primary, 0.72);
        var control = Mix(background, primary, 0.11);
        var accent = EnsureContrast(requestedAccent, background, control, primary, 3);
        var stroke = Color.FromArgb(66, primary.R, primary.G, primary.B);

        return new SolidThemeContrastPalette(primary, secondary, accent, control, stroke);
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
        for (var step = 0; step <= 20; step++)
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
