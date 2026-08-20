using System.Globalization;
using System.Text.RegularExpressions;

namespace MiaDock.Core.Clipboard;

/// <summary>Normalised CSS colour values suitable for one-tap clipboard copying.</summary>
public sealed partial record ClipboardColorFormats(
    string Hex,
    string Rgb,
    string Hsl,
    string RgbChannelsDisplay,
    string HslDisplay,
    byte Red,
    byte Green,
    byte Blue,
    byte Alpha)
{
    public static bool TryFromHex(string? hex, out ClipboardColorFormats formats) => TryParse(hex, out formats);

    public static bool TryParse(string? value, out ClipboardColorFormats formats)
    {
        formats = null!;
        var text = value?.Trim();
        if (string.IsNullOrEmpty(text)) return false;
        if (TryParseHex(text, out var red, out var green, out var blue, out var alpha) ||
            TryParseRgb(text, out red, out green, out blue, out alpha) ||
            TryParseHsl(text, out red, out green, out blue, out alpha))
        {
            formats = FromRgba(red, green, blue, alpha);
            return true;
        }
        return false;
    }

    public static bool TryConvertHslToHex(string value, out string hex)
    {
        hex = string.Empty;
        if (!TryParse(value, out var formats)) return false;
        hex = formats.Hex;
        return true;
    }

    private static ClipboardColorFormats FromRgba(byte red, byte green, byte blue, byte alpha)
    {
        ToHsl(red, green, blue, out var hue, out var saturation, out var lightness);
        var alphaText = alpha == byte.MaxValue ? null : FormatAlpha(alpha);
        return new(
            alphaText is null ? $"#{red:X2}{green:X2}{blue:X2}" : $"#{red:X2}{green:X2}{blue:X2}{alpha:X2}",
            alphaText is null ? $"rgb({red}, {green}, {blue})" : $"rgba({red}, {green}, {blue}, {alphaText})",
            alphaText is null ? $"hsl({hue}, {saturation}%, {lightness}%)" : $"hsla({hue}, {saturation}%, {lightness}%, {alphaText})",
            alphaText is null ? $"{red}, {green}, {blue}" : $"{red}, {green}, {blue}, {alphaText}",
            alphaText is null ? $"{hue}°, {saturation}%, {lightness}%" : $"{hue}°, {saturation}%, {lightness}%, {alphaText}",
            red, green, blue, alpha);
    }

    private static bool TryParseHex(string value, out byte red, out byte green, out byte blue, out byte alpha)
    {
        red = green = blue = 0;
        alpha = byte.MaxValue;
        if (value.Length is not (4 or 5 or 7 or 9) || value[0] != '#') return false;
        var digits = value[1..];
        if (digits.Length is 3 or 4)
        {
            if (!TryParseNibble(digits[0], out red) || !TryParseNibble(digits[1], out green) ||
                !TryParseNibble(digits[2], out blue) || (digits.Length == 4 && !TryParseNibble(digits[3], out alpha))) return false;
            return true;
        }
        return byte.TryParse(digits.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out red) &&
               byte.TryParse(digits.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out green) &&
               byte.TryParse(digits.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out blue) &&
               (digits.Length == 6 || byte.TryParse(digits.AsSpan(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out alpha));
    }

    private static bool TryParseNibble(char value, out byte channel)
    {
        channel = 0;
        if (!byte.TryParse(value.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var nibble)) return false;
        channel = (byte)((nibble << 4) | nibble);
        return true;
    }

    private static bool TryParseRgb(string value, out byte red, out byte green, out byte blue, out byte alpha)
    {
        red = green = blue = 0;
        alpha = byte.MaxValue;
        var match = RgbRegex().Match(value);
        if (!match.Success) return false;
        var parts = match.Groups[2].Value.Split(',').Select(static part => part.Trim()).ToArray();
        if (parts.Length != (match.Groups[1].Value.Equals("rgba", StringComparison.OrdinalIgnoreCase) ? 4 : 3) ||
            !TryParseChannel(parts[0], out red) || !TryParseChannel(parts[1], out green) || !TryParseChannel(parts[2], out blue)) return false;
        return parts.Length == 3 || TryParseAlpha(parts[3], out alpha);
    }

    private static bool TryParseHsl(string value, out byte red, out byte green, out byte blue, out byte alpha)
    {
        red = green = blue = 0;
        alpha = byte.MaxValue;
        var match = HslRegex().Match(value);
        if (!match.Success) return false;
        var parts = match.Groups[2].Value.Split(',').Select(static part => part.Trim()).ToArray();
        if (parts.Length != (match.Groups[1].Value.Equals("hsla", StringComparison.OrdinalIgnoreCase) ? 4 : 3)) return false;
        var hueText = parts[0];
        if (hueText.EndsWith("deg", StringComparison.OrdinalIgnoreCase)) hueText = hueText[..^3].Trim();
        if (!double.TryParse(hueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var hue) ||
            !TryParsePercent(parts[1], out var saturation) || !TryParsePercent(parts[2], out var lightness) ||
            saturation is < 0 or > 100 || lightness is < 0 or > 100 ||
            (parts.Length == 4 && !TryParseAlpha(parts[3], out alpha))) return false;
        hue %= 360d;
        if (hue < 0) hue += 360d;
        ToRgb(hue, saturation / 100d, lightness / 100d, out red, out green, out blue);
        return true;
    }

    private static bool TryParseChannel(string value, out byte channel) => byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out channel);

    private static bool TryParseAlpha(string value, out byte alpha)
    {
        alpha = 0;
        var text = value.Trim();
        if (text.EndsWith('%'))
        {
            if (!double.TryParse(text[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) || percent is < 0 or > 100) return false;
            alpha = (byte)Math.Round(percent * 2.55d, MidpointRounding.AwayFromZero);
            return true;
        }
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var normalized) || normalized is < 0 or > 1) return false;
        alpha = (byte)Math.Round(normalized * byte.MaxValue, MidpointRounding.AwayFromZero);
        return true;
    }

    private static bool TryParsePercent(string value, out double percent)
    {
        percent = 0;
        var text = value.Trim();
        return text.EndsWith('%') && double.TryParse(text[..^1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out percent);
    }

    private static string FormatAlpha(byte alpha) => (alpha / 255d).ToString("0.###", CultureInfo.InvariantCulture);

    private static void ToHsl(byte red, byte green, byte blue, out int hue, out int saturation, out int lightness)
    {
        var r = red / 255d; var g = green / 255d; var b = blue / 255d;
        var max = Math.Max(r, Math.Max(g, b)); var min = Math.Min(r, Math.Min(g, b));
        var lightnessValue = (max + min) / 2d; var hueValue = 0d; var saturationValue = 0d;
        if (max != min)
        {
            var delta = max - min;
            saturationValue = lightnessValue > .5d ? delta / (2d - max - min) : delta / (max + min);
            hueValue = max == r ? (g - b) / delta + (g < b ? 6 : 0) : max == g ? (b - r) / delta + 2 : (r - g) / delta + 4;
            hueValue *= 60d;
        }
        hue = ((int)Math.Round(hueValue, MidpointRounding.AwayFromZero)) % 360;
        saturation = (int)Math.Round(saturationValue * 100d, MidpointRounding.AwayFromZero);
        lightness = (int)Math.Round(lightnessValue * 100d, MidpointRounding.AwayFromZero);
    }

    private static void ToRgb(double hue, double saturation, double lightness, out byte red, out byte green, out byte blue)
    {
        var chroma = (1d - Math.Abs(2d * lightness - 1d)) * saturation;
        var x = chroma * (1d - Math.Abs((hue / 60d) % 2d - 1d)); var m = lightness - chroma / 2d;
        var (r, g, b) = hue switch { < 60 => (chroma, x, 0d), < 120 => (x, chroma, 0d), < 180 => (0d, chroma, x), < 240 => (0d, x, chroma), < 300 => (x, 0d, chroma), _ => (chroma, 0d, x) };
        red = (byte)Math.Clamp((int)Math.Round((r + m) * 255d, MidpointRounding.AwayFromZero), 0, 255);
        green = (byte)Math.Clamp((int)Math.Round((g + m) * 255d, MidpointRounding.AwayFromZero), 0, 255);
        blue = (byte)Math.Clamp((int)Math.Round((b + m) * 255d, MidpointRounding.AwayFromZero), 0, 255);
    }

    [GeneratedRegex("^(rgb|rgba)\\((.*)\\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex RgbRegex();
    [GeneratedRegex("^(hsl|hsla)\\((.*)\\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex HslRegex();
}
