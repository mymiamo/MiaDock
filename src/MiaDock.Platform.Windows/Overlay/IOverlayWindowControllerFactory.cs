using Microsoft.UI.Xaml;

namespace MiaDock.Platform.Windows.Overlay;

public interface IOverlayWindowControllerFactory
{
    IOverlayWindowController Create(Window window, OverlayWindowOptions options);
}
