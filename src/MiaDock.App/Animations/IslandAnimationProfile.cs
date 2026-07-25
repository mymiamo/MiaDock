using MiaDock.Core.Presentation;

namespace MiaDock.App.Animations;

public static class IslandAnimationProfile
{
    public static IslandVisualMetrics ForState(
        IslandVisualState state,
        IslandLayoutOptions? layoutOptions = null)
    {
        var layout = layoutOptions ?? IslandLayoutOptions.Default;
        return state switch
        {
            IslandVisualState.Collapsed => new(
                layout.CollapsedWidth,
                layout.CollapsedHeight,
                Math.Min(layout.CornerRadius, layout.CollapsedHeight / 2)),
            IslandVisualState.Hover => new(
                layout.HoverWidth,
                layout.HoverHeight,
                Math.Min(layout.CornerRadius, layout.HoverHeight / 2)),
            IslandVisualState.ExpandedModule => new(
                layout.ExpandedWidth,
                layout.ExpandedHeight,
                Math.Min(layout.CornerRadius, layout.ExpandedHeight / 2)),
            IslandVisualState.ModuleNotification => new(
                layout.NotificationWidth,
                layout.NotificationHeight,
                Math.Min(layout.CornerRadius, layout.NotificationHeight / 2)),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }

    public static TimeSpan DurationFor(IslandTransition transition, IslandMotionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!transition.Changed && transition.Trigger == IslandTrigger.ModuleEventReceived)
        {
            return options.ContentRefreshDuration;
        }

        if (transition.CurrentState == IslandVisualState.ModuleNotification)
        {
            return options.NotificationEnterDuration;
        }

        if (transition.PreviousState == IslandVisualState.ModuleNotification)
        {
            return options.NotificationExitDuration;
        }

        return transition.CurrentState switch
        {
            IslandVisualState.Hover => options.HoverDuration,
            IslandVisualState.ExpandedModule => options.ExpandDuration,
            IslandVisualState.Collapsed => options.CollapseDuration,
            _ => options.ExpandDuration
        };
    }
}
