namespace MiaDock.Core.Presentation;

public sealed class IslandStateMachine : IIslandStateMachine
{
    public IslandVisualState CurrentState { get; private set; } = IslandVisualState.Collapsed;

    public bool IsPointerOver { get; private set; }

    public IslandTransition Dispatch(IslandTrigger trigger)
    {
        var previousState = CurrentState;

        UpdatePointerState(trigger);
        CurrentState = ResolveState(CurrentState, trigger);

        return new IslandTransition(previousState, CurrentState, trigger);
    }

    private void UpdatePointerState(IslandTrigger trigger)
    {
        if (trigger == IslandTrigger.PointerEntered)
        {
            IsPointerOver = true;
        }
        else if (trigger == IslandTrigger.PointerExited)
        {
            IsPointerOver = false;
        }
    }

    private IslandVisualState ResolveState(IslandVisualState state, IslandTrigger trigger) => trigger switch
    {
        IslandTrigger.TrackChanged when state != IslandVisualState.ExpandedMusic => IslandVisualState.TrackNotification,
        IslandTrigger.PrimaryInvoked => IslandVisualState.ExpandedMusic,
        IslandTrigger.CollapseRequested => RestingState,
        IslandTrigger.NotificationElapsed when state == IslandVisualState.TrackNotification => RestingState,
        IslandTrigger.PointerEntered when state == IslandVisualState.Collapsed => IslandVisualState.Hover,
        IslandTrigger.PointerExited when state == IslandVisualState.Hover => IslandVisualState.Collapsed,
        _ => state
    };

    private IslandVisualState RestingState => IsPointerOver
        ? IslandVisualState.Hover
        : IslandVisualState.Collapsed;
}
