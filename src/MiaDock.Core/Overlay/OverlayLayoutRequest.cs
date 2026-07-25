namespace MiaDock.Core.Overlay;

public sealed record OverlayLayoutRequest(
    OverlayWorkArea WorkArea,
    OverlaySize SizeInDips,
    uint Dpi,
    OverlayPosition Position,
    double MarginInDips = 12);
