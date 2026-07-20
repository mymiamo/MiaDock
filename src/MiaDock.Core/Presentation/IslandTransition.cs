namespace MiaDock.Core.Presentation;

public readonly record struct IslandTransition(
    IslandVisualState PreviousState,
    IslandVisualState CurrentState,
    IslandTrigger Trigger)
{
    public bool Changed => PreviousState != CurrentState;
}
