using Microsoft.UI.Xaml;
using MiaDock.Core.Presentation;

namespace MiaDock.App.Animations;

public interface IIslandAnimationCoordinator : IDisposable
{
    bool IsAnimating { get; }

    void ApplyInitialState(IslandVisualState state);

    void RequestTransition(IslandTransition transition);

    void UpdateOptions(IslandMotionOptions options, IslandLayoutOptions layoutOptions);

    void RequestLayoutTransition(IslandLayoutOptions layoutOptions);

    void RequestContentTransition(FrameworkElement element, MotionDirection direction);

    void RequestContentRefresh(FrameworkElement element);
}
