using MiaDock.Platform.Windows.Interop;
using MiaDock.Core.Presentation;

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

        return Contains(x, y, width, height, DockCornerRadii.Uniform(cornerRadius));
    }

    public static bool Contains(
        double x,
        double y,
        double width,
        double height,
        DockCornerRadii cornerRadii)
    {
        if (width <= 0 || height <= 0 ||
            x < 0 || y < 0 || x >= width || y >= height)
        {
            return false;
        }

        var radii = cornerRadii.Clamp(0, Math.Min(width, height) / 2);
        var radius = x < width / 2
            ? y < height / 2 ? radii.TopLeft : radii.BottomLeft
            : y < height / 2 ? radii.TopRight : radii.BottomRight;
        if (radius <= 0)
        {
            return true;
        }

        var leftCorner = x < width / 2;
        var topCorner = y < height / 2;
        if ((!leftCorner && x < width - radius) ||
            (leftCorner && x >= radius) ||
            (!topCorner && y < height - radius) ||
            (topCorner && y >= radius))
        {
            return true;
        }

        var centerX = leftCorner ? radius : width - radius;
        var centerY = topCorner ? radius : height - radius;
        var deltaX = x + 0.5 - centerX;
        var deltaY = y + 0.5 - centerY;
        return deltaX * deltaX + deltaY * deltaY <= radius * radius;
    }
}
