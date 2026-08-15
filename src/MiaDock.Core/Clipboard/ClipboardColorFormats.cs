using System.Globalization;

namespace MiaDock.Core.Clipboard;

public sealed record ClipboardColorFormats(
    string Hex,
    string Rgb,
    string Hsl,
    string RgbChannelsDisplay,
    string HslDisplay)
{
    public static bool TryFromHex(string? hex, out ClipboardColorFormats formats)
    {
        formats = null!;
        if (hex is not { Length: 7 } || hex[0] != '#') return false;
        if (!byte.TryParse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var red) ||
            !byte.TryParse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var green) ||
            !byte.TryParse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var blue))
            return false;

        formats = FromRgb(red, green, blue);
        return true;
    }

    public static bool TryConvertHslToHex(string value, out string hex)
    {
        hex = string.Empty;
        if (value.Length < 8 ||
            !value.StartsWith("hsl(", StringComparison.OrdinalIgnoreCase) ||
            value[^1] != ')')
            return false;

        var parts = value[4..^1].Split(',');
        if (parts.Length != 3) return false;

        var hueText = parts[0].Trim();
        if (hueText.EndsWith("deg", StringComparison.OrdinalIgnoreCase))
            hueText = hueText[..^3].Trim();
        if (!double.TryParse(hueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var hue) ||
            !TryParsePercent(parts[1], out var saturation) ||
            !TryParsePercent(parts[2], out var lightness) ||
            saturation is < 0 or > 100 ||
            lightness is < 0 or > 100)
            return false;

        hue %= 360.0;
        if (hue < 0) hue += 360.0;
        ToRgb(hue, saturation / 100.0, lightness / 100.0, out var red, out var green, out var blue);
        hex = $"#{red:X2}{green:X2}{blue:X2}";
        return true;
    }

    private static ClipboardColorFormats FromRgb(byte red, byte green, byte blue)
    {
        ToHsl(red, green, blue, out var hue, out var saturation, out var lightness);
        return new(
            $"#{red:X2}{green:X2}{blue:X2}",
            $"rgb({red}, {green}, {blue})",
            $"hsl({hue}, {saturation}%, {lightness}%)",
            $"{red}, {green}, {blue}",
            $"{hue}°, {saturation}%, {lightness}%");
    }

    private static bool TryParsePercent(string value, out double percent)
    {
        percent = 0;
        var text = value.Trim();
        if (!text.EndsWith('%')) return false;
        return double.TryParse(text[..^1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out percent);
    }

    private static void ToHsl(byte red, byte green, byte blue, out int hue, out int saturation, out int lightness)
    {
        var r = red / 255.0;
        var g = green / 255.0;
        var b = blue / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var lightnessValue = (max + min) / 2.0;
        double hueValue = 0;
        double saturationValue = 0;
        if (max != min)
        {
            var delta = max - min;
            saturationValue = lightnessValue > 0.5
                ? delta / (2.0 - max - min)
                : delta / (max + min);
            if (max == r)
                hueValue = (g - b) / delta + (g < b ? 6 : 0);
            else if (max == g)
                hueValue = (b - r) / delta + 2;
            else
                hueValue = (r - g) / delta + 4;
            hueValue *= 60.0;
        }

        hue = (int)Math.Round(hueValue, MidpointRounding.AwayFromZero);
        if (hue == 360) hue = 0;
        saturation = ClampPercent(saturationValue * 100.0);
        lightness = ClampPercent(lightnessValue * 100.0);
    }

    private static void ToRgb(double hue, double saturation, double lightness, out byte red, out byte green, out byte blue)
    {
        double r;
        double g;
        double b;
        if (saturation == 0)
        {
            r = g = b = lightness;
        }
        else
        {
            var q = lightness < 0.5
                ? lightness * (1 + saturation)
                : lightness + saturation - lightness * saturation;
            var p = 2 * lightness - q;
            var h = hue / 360.0;
            r = HueToRgb(p, q, h + 1.0 / 3.0);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3.0);
        }

        red = ToByte(r);
        green = ToByte(g);
        blue = ToByte(b);
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 0.5) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }

    private static int ClampPercent(double value) =>
        Math.Clamp((int)Math.Round(value, MidpointRounding.AwayFromZero), 0, 100);

    private static byte ToByte(double channel) =>
        (byte)Math.Clamp((int)Math.Round(channel * 255.0, MidpointRounding.AwayFromZero), 0, 255);
}
