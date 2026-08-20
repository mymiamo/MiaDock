using MiaDock.Core.Overlay;
using MiaDock.Core.Presentation;

namespace MiaDock.Platform.Windows.Overlay;

public interface IOverlayWindowController : IDisposable
{
    event EventHandler? OutsidePointerPressed;

    nint WindowHandle { get; }

    Exception? LastFailure { get; }

    bool IsVisible { get; }

    void ShowNoActivate();

    void Hide();

    void UpdatePlacement(OverlayPosition position, string? displayId, double marginInDips);

    void UpdateLayout(OverlaySize sizeInDips, DockCornerRadii cornerRadiiInDips);

    void UpdateOpacity(double opacity);

    void SetOutsideClickMonitoring(bool enabled);

    void SetInputActivationEnabled(bool enabled);

    void SetEdgeRevealHidden(
        bool hidden,
        double visibleStripInDips = 2,
        bool animate = false,
        Action? transitionCompleted = null);

    bool IsPointerAtAttachedEdge(int activationThicknessInPixels = 3, int spanPaddingInPixels = 12);

    bool IsPointerOverWindow();
}
