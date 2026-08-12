using System.ComponentModel;
using MiaDock.Core.Presentation;
using MiaDock.Platform.Windows.Interop;

namespace MiaDock.Platform.Windows.Overlay;

internal static class RoundedRegionBuilder
{
    internal static nint Create(
        int width,
        int height,
        int inset,
        DockCornerRadii radii)
    {
        var innerWidth = width - inset * 2;
        var innerHeight = height - inset * 2;
        if (innerWidth <= 0 || innerHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(inset));
        }

        var normalized = radii.Clamp(0, Math.Min(innerWidth, innerHeight) / 2d);
        if (normalized.IsUniform())
        {
            var diameter = Math.Max(1, checked((int)Math.Round(normalized.TopLeft * 2)));
            return NativeMethods.CreateRoundRectRgn(
                inset,
                inset,
                width - inset,
                height - inset,
                diameter,
                diameter);
        }

        var result = NativeMethods.CreateRectRgn(0, 0, 0, 0);
        if (result == 0)
        {
            return 0;
        }

        try
        {
            var startY = 0;
            var previous = HorizontalSpan(innerWidth, innerHeight, normalized, 0);
            for (var y = 1; y <= innerHeight; y++)
            {
                var next = y < innerHeight
                    ? HorizontalSpan(innerWidth, innerHeight, normalized, y)
                    : default;
                if (y < innerHeight && next == previous)
                {
                    continue;
                }

                var rowRegion = NativeMethods.CreateRectRgn(
                    inset + previous.Left,
                    inset + startY,
                    inset + previous.Right,
                    inset + y);
                if (rowRegion == 0)
                {
                    throw new Win32Exception(
                        System.Runtime.InteropServices.Marshal.GetLastPInvokeError(),
                        "Unable to create an asymmetric dock region row.");
                }

                try
                {
                    if (NativeMethods.CombineRgn(
                            result,
                            result,
                            rowRegion,
                            NativeConstants.RgnOr) == NativeConstants.RgnError)
                    {
                        throw new Win32Exception(
                            System.Runtime.InteropServices.Marshal.GetLastPInvokeError(),
                            "Unable to combine the asymmetric dock region.");
                    }
                }
                finally
                {
                    _ = NativeMethods.DeleteObject(rowRegion);
                }

                startY = y;
                previous = next;
            }

            var completed = result;
            result = 0;
            return completed;
        }
        finally
        {
            if (result != 0)
            {
                _ = NativeMethods.DeleteObject(result);
            }
        }
    }

    private static (int Left, int Right) HorizontalSpan(
        int width,
        int height,
        DockCornerRadii radii,
        int y)
    {
        var sampleY = y + 0.5;
        var left = sampleY < radii.TopLeft
            ? LeftBoundary(radii.TopLeft, sampleY)
            : sampleY > height - radii.BottomLeft
                ? LeftBoundary(radii.BottomLeft, height - sampleY)
                : 0;
        var right = sampleY < radii.TopRight
            ? width - LeftBoundary(radii.TopRight, sampleY)
            : sampleY > height - radii.BottomRight
                ? width - LeftBoundary(radii.BottomRight, height - sampleY)
                : width;
        return (Math.Clamp(left, 0, width), Math.Clamp(right, left + 1, width));
    }

    private static int LeftBoundary(double radius, double distanceFromEdge)
    {
        if (radius <= 0)
        {
            return 0;
        }

        var delta = distanceFromEdge - radius;
        var horizontal = Math.Sqrt(Math.Max(0, radius * radius - delta * delta));
        return Math.Max(0, checked((int)Math.Ceiling(radius - horizontal - 0.5)));
    }
}
