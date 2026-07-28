namespace MiaDock.Platform.Windows.Overlay;

internal static class RoundedRectangleRasterizer
{
    internal static int[] RenderPremultipliedBgra(
        int width,
        int height,
        double radius,
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

        if (!double.IsFinite(radius) || radius < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        if (double.IsNaN(edgeThickness) || edgeThickness <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(edgeThickness));
        }

        var clampedRadius = Math.Min(radius, Math.Min(width, height) / 2d);
        var baseAlpha = (byte)(argb >> 24);
        var red = (byte)(argb >> 16);
        var green = (byte)(argb >> 8);
        var blue = (byte)argb;
        var pixels = new int[checked(width * height)];
        var rendersEdgeOnly = double.IsFinite(edgeThickness) &&
                              edgeThickness < Math.Min(width, height) / 2d;
        var innerWidth = Math.Max(0, width - edgeThickness * 2);
        var innerHeight = Math.Max(0, height - edgeThickness * 2);
        var innerRadius = Math.Max(0, clampedRadius - edgeThickness);

        for (var y = 0; y < height; y++)
        {
            var pixelY = y + 0.5;
            for (var x = 0; x < width; x++)
            {
                var pixelX = x + 0.5;
                var coverage = Coverage(width, height, clampedRadius, pixelX, pixelY);
                if (rendersEdgeOnly)
                {
                    var innerCoverage = Coverage(
                        innerWidth,
                        innerHeight,
                        innerRadius,
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
        double radius,
        double pixelX,
        double pixelY)
    {
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        var halfWidth = width / 2d;
        var halfHeight = height / 2d;
        var clampedRadius = Math.Min(radius, Math.Min(halfWidth, halfHeight));
        var qx = Math.Abs(pixelX - halfWidth) - Math.Max(0, halfWidth - clampedRadius);
        var qy = Math.Abs(pixelY - halfHeight) - Math.Max(0, halfHeight - clampedRadius);
        var outsideX = Math.Max(qx, 0);
        var outsideY = Math.Max(qy, 0);
        var signedDistance =
            Math.Sqrt(outsideX * outsideX + outsideY * outsideY) +
            Math.Min(Math.Max(qx, qy), 0) -
            clampedRadius;
        return Math.Clamp(0.5 - signedDistance, 0, 1);
    }
}
