using Microsoft.UI.Xaml;

namespace MiaDock.App.Animations;

/// <summary>
/// Coordinates short, stateful control feedback without competing with the
/// island shell transition coordinator.
/// </summary>
public interface IMicroInteractionCoordinator : IDisposable
{
    void PlayPress(FrameworkElement target);

    void PlayStateChange(FrameworkElement target);

    void PlayAttention(FrameworkElement target);

    void PlayNotification(FrameworkElement target);

    void Cancel(FrameworkElement target);
}
