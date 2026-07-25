using MiaDock.Core.Overlay;

namespace MiaDock.Platform.Windows.Overlay;

public interface IOverlayWindowController : IDisposable
{
    event EventHandler? OutsidePointerPressed;

    nint WindowHandle { get; }

    Exception? LastFailure { get; }

    bool IsVisible { get; }

    void ShowNoActivate();

    void Hide();

    void UpdatePlacement(OverlayPosition position, string? displayId);

    void UpdateLayout(OverlaySize sizeInDips, double cornerRadiusInDips);

    void UpdateOpacity(double opacity);

    void SetOutsideClickMonitoring(bool enabled);
}
