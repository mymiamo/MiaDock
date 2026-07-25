using MiaDock.Core.Overlay;

namespace MiaDock.Platform.Windows.Overlay;

public sealed record OverlayWindowOptions(
    OverlayPosition Position,
    OverlaySize InitialSize,
    double MarginInDips,
    double CornerRadiusInDips)
{
    public static OverlayWindowOptions Default { get; } = new(
        OverlayPosition.TopCenter,
        new OverlaySize(292, 46),
        12,
        22);
}
