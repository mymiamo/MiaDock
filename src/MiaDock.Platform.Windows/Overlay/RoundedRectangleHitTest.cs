using MiaDock.Platform.Windows.Interop;

namespace MiaDock.Platform.Windows.Overlay;

internal static class RoundedRectangleHitTest
{
    public static NativePoint PointFromMessage(nint messageParameter)
    {
        var value = messageParameter.ToInt64();
        return new NativePoint
        {
            X = unchecked((short)(value & 0xFFFF)),
            Y = unchecked((short)((value >> 16) & 0xFFFF))
        };
    }

    public static bool Contains(
        double x,
        double y,
        double width,
        double height,
        double cornerRadius)
    {
        if (width <= 0 || height <= 0 ||
            x < 0 || y < 0 || x >= width || y >= height)
        {
            return false;
        }

        var radius = Math.Clamp(cornerRadius, 0, Math.Min(width, height) / 2);
        if (radius <= 0 ||
            (x >= radius && x < width - radius) ||
            (y >= radius && y < height - radius))
        {
            return true;
        }

        var centerX = x < radius ? radius : width - radius;
        var centerY = y < radius ? radius : height - radius;
        var deltaX = x + 0.5 - centerX;
        var deltaY = y + 0.5 - centerY;
        return deltaX * deltaX + deltaY * deltaY <= radius * radius;
    }
}
