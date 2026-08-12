using MiaDock.Core.Overlay;
using MiaDock.Core.Presentation;

namespace MiaDock.Platform.Windows.Overlay;

public sealed record OverlayWindowOptions(
    OverlayPosition Position,
    OverlaySize InitialSize,
    double MarginInDips,
    DockCornerRadii CornerRadiiInDips)
{
    public static OverlayWindowOptions Default { get; } = new(
        OverlayPosition.TopCenter,
        new OverlaySize(292, 46),
        12,
        DockCornerRadii.Uniform(23));
}
