namespace MiaDock.Core.Presentation;

public interface IIslandStateMachine
{
    IslandVisualState CurrentState { get; }

    bool IsPointerOver { get; }

    IslandTransition Dispatch(IslandTrigger trigger);
}
