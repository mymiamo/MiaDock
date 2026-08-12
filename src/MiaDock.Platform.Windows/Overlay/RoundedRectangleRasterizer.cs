using MiaDock.Core.Presentation;

namespace MiaDock.Platform.Windows.Overlay;

internal static class RoundedRectangleRasterizer
{
    internal static int[] RenderPremultipliedBgra(
        int width,
        int height,
        double radius,
        uint argb,
        double edgeThickness = double.PositiveInfinity)
        => RenderPremultipliedBgra(
            width,
            height,
            DockCornerRadii.Uniform(radius),
            argb,
            edgeThickness);

    internal static int[] RenderPremultipliedBgra(
        int width,
        int height,
        DockCornerRadii radii,
        uint argb,
        double edgeThickness = double.PositiveInfinity)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (!double.IsFinite(radii.TopLeft) || radii.TopLeft < 0 ||
            !double.IsFinite(radii.TopRight) || radii.TopRight < 0 ||
            !double.IsFinite(radii.BottomRight) || radii.BottomRight < 0 ||
            !double.IsFinite(radii.BottomLeft) || radii.BottomLeft < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radii));
        }

        if (double.IsNaN(edgeThickness) || edgeThickness <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(edgeThickness));
        }

        var clampedRadii = radii.Clamp(0, Math.Min(width, height) / 2d);
        var baseAlpha = (byte)(argb >> 24);
        var red = (byte)(argb >> 16);
        var green = (byte)(argb >> 8);
        var blue = (byte)argb;
        var pixels = new int[checked(width * height)];
        var rendersEdgeOnly = double.IsFinite(edgeThickness) &&
                              edgeThickness < Math.Min(width, height) / 2d;
        var innerWidth = Math.Max(0, width - edgeThickness * 2);
        var innerHeight = Math.Max(0, height - edgeThickness * 2);
        var innerRadii = new DockCornerRadii(
            Math.Max(0, clampedRadii.TopLeft - edgeThickness),
            Math.Max(0, clampedRadii.TopRight - edgeThickness),
            Math.Max(0, clampedRadii.BottomRight - edgeThickness),
            Math.Max(0, clampedRadii.BottomLeft - edgeThickness));

        for (var y = 0; y < height; y++)
        {
            var pixelY = y + 0.5;
            for (var x = 0; x < width; x++)
            {
                var pixelX = x + 0.5;
                var coverage = Coverage(width, height, clampedRadii, pixelX, pixelY);
                if (rendersEdgeOnly)
                {
                    var innerCoverage = Coverage(
                        innerWidth,
                        innerHeight,
                        innerRadii,
                        pixelX - edgeThickness,
                        pixelY - edgeThickness);
                    coverage = Math.Clamp(coverage - innerCoverage, 0, 1);
                }

                var alpha = checked((byte)Math.Round(baseAlpha * coverage));
                if (alpha == 0)
                {
                    continue;
                }

                var premultipliedRed = checked((byte)Math.Round(red * alpha / 255d));
                var premultipliedGreen = checked((byte)Math.Round(green * alpha / 255d));
                var premultipliedBlue = checked((byte)Math.Round(blue * alpha / 255d));
                pixels[y * width + x] =
                    alpha << 24 |
                    premultipliedRed << 16 |
                    premultipliedGreen << 8 |
                    premultipliedBlue;
            }
        }

        return pixels;
    }

    private static double Coverage(
        double width,
        double height,
        DockCornerRadii radii,
        double pixelX,
        double pixelY)
    {
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        var normalized = radii.Clamp(0, Math.Min(width, height) / 2d);
        var left = pixelX < width / 2d;
        var top = pixelY < height / 2d;
        var radius = left
            ? top ? normalized.TopLeft : normalized.BottomLeft
            : top ? normalized.TopRight : normalized.BottomRight;
        if (radius <= 0)
        {
            return 1;
        }

        var centerX = left ? radius : width - radius;
        var centerY = top ? radius : height - radius;
        var inCornerSquare = left ? pixelX < radius : pixelX > width - radius;
        inCornerSquare &= top ? pixelY < radius : pixelY > height - radius;
        if (!inCornerSquare)
        {
            return 1;
        }

        var deltaX = pixelX - centerX;
        var deltaY = pixelY - centerY;
        return Math.Clamp(radius + 0.5 - Math.Sqrt(deltaX * deltaX + deltaY * deltaY), 0, 1);
    }
}
