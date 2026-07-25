using Windows.UI;

namespace MiaDock.App.Services;

public static class ColorParser
{
    public static Color ParseRgb(string value)
    {
        var normalized = value.TrimStart('#');
        return Color.FromArgb(
            255,
            Convert.ToByte(normalized[0..2], 16),
            Convert.ToByte(normalized[2..4], 16),
            Convert.ToByte(normalized[4..6], 16));
    }

    public static Color WithOpacity(Color color, double opacity) => Color.FromArgb(
        checked((byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255)),
        color.R,
        color.G,
        color.B);
}
