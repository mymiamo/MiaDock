using MiaDock.Core.Presentation;

namespace MiaDock.App.Animations;

public interface IIslandAnimationCoordinator : IDisposable
{
    bool IsAnimating { get; }

    void ApplyInitialState(IslandVisualState state);

    void RequestTransition(IslandTransition transition);
}
