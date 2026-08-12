namespace MiaDock.Core.Overlay;

public sealed class OverlayPlacementCalculator : IOverlayPlacementCalculator
{
    private const double DefaultDpi = 96;

    public OverlayPlacement Calculate(OverlayLayoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.WorkArea.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The work area must have positive dimensions.");
        }

        if (!request.SizeInDips.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The overlay size must be finite and positive.");
        }

        if (request.Dpi == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "DPI must be greater than zero.");
        }

        if (!double.IsFinite(request.MarginInDips) || request.MarginInDips < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "The margin must be finite and non-negative.");
        }

        var scale = request.Dpi / DefaultDpi;
        var width = Math.Max(1, Scale(request.SizeInDips.Width, scale));
        var height = Math.Max(1, Scale(request.SizeInDips.Height, scale));
        var margin = Scale(request.MarginInDips, scale);

        var left = request.WorkArea.X;
        var top = request.WorkArea.Y;
        var right = left + request.WorkArea.Width;
        var bottom = top + request.WorkArea.Height;

        var x = request.Position switch
        {
            OverlayPosition.TopLeft or OverlayPosition.BottomLeft or OverlayPosition.LeftCenter => left + margin,
            OverlayPosition.TopCenter or OverlayPosition.BottomCenter => left + ((request.WorkArea.Width - width) / 2),
            OverlayPosition.TopRight or OverlayPosition.BottomRight or OverlayPosition.RightCenter => right - width - margin,
            _ => throw new ArgumentOutOfRangeException(nameof(request), "Unknown overlay position.")
        };

        var y = request.Position switch
        {
            OverlayPosition.TopCenter or OverlayPosition.TopLeft or OverlayPosition.TopRight => top + margin,
            OverlayPosition.BottomCenter or OverlayPosition.BottomLeft or OverlayPosition.BottomRight => bottom - height - margin,
            OverlayPosition.LeftCenter or OverlayPosition.RightCenter => top + ((request.WorkArea.Height - height) / 2),
            _ => throw new ArgumentOutOfRangeException(nameof(request), "Unknown overlay position.")
        };

        var maximumX = Math.Max(left, right - width);
        var maximumY = Math.Max(top, bottom - height);

        return new OverlayPlacement(
            Math.Clamp(x, left, maximumX),
            Math.Clamp(y, top, maximumY),
            width,
            height);
    }

    private static int Scale(double value, double scale) =>
        checked((int)Math.Round(value * scale, MidpointRounding.AwayFromZero));
}
