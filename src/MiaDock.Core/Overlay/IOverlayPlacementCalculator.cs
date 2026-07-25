namespace MiaDock.Core.Overlay;

public interface IOverlayPlacementCalculator
{
    OverlayPlacement Calculate(OverlayLayoutRequest request);
}
