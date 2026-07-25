using Microsoft.UI.Xaml;
using MiaDock.Core.Overlay;
using MiaDock.Platform.Windows.Display;

namespace MiaDock.Platform.Windows.Overlay;

public sealed class OverlayWindowControllerFactory(
    IOverlayPlacementCalculator placementCalculator,
    IDisplayTopologyService displayTopology)
    : IOverlayWindowControllerFactory
{
    public IOverlayWindowController Create(Window window, OverlayWindowOptions options) =>
        new OverlayWindowController(window, options, placementCalculator, displayTopology);
}
