using MiaDock.Core.Presentation;

namespace MiaDock.App.Animations;

public static class IslandAnimationProfile
{
    public static IslandVisualMetrics ForState(
        IslandVisualState state,
        IslandLayoutOptions? layoutOptions = null)
    {
        var layout = layoutOptions ?? IslandLayoutOptions.Default;
        var radii = layout.EffectiveCornerRadii;
        return state switch
        {
            IslandVisualState.Collapsed => new(
                layout.CollapsedWidth,
                layout.CollapsedHeight,
                radii.Clamp(0, layout.CollapsedHeight / 2)),
            IslandVisualState.Hover => new(
                layout.HoverWidth,
                layout.HoverHeight,
                radii.Clamp(0, layout.HoverHeight / 2)),
            IslandVisualState.ExpandedModule => new(
                layout.ExpandedWidth,
                layout.ExpandedHeight,
                radii.Clamp(0, layout.ExpandedHeight / 2)),
            IslandVisualState.ModuleNotification => new(
                layout.NotificationWidth,
                layout.NotificationHeight,
                radii.Clamp(0, layout.NotificationHeight / 2)),
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

    /// <summary>
    /// Compact/Hover ↔ ModuleNotification morph used by transient dock events
    /// (privacy, bluetooth, battery, timer, transfers, media, Windows notifications).
    /// </summary>
    public static bool IsEventMorph(IslandTransition transition) =>
        transition.Changed &&
        (transition.CurrentState == IslandVisualState.ModuleNotification ||
         transition.PreviousState == IslandVisualState.ModuleNotification);

    public static BoundsEasingProfile BoundsEasingFor(
        IslandTransition transition,
        IslandMotionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!IsEventMorph(transition) || options.Preset == MotionPreset.Off)
        {
            return BoundsEasingProfile.Cubic;
        }

        // Event morphs always use a soft spring on bounds so the card
        // shape itself elastic-morphs; content still respects AnimationKind.
        var springiness = options.Preset switch
        {
            MotionPreset.Minimal => Math.Min(options.Springiness, 0.25),
            MotionPreset.Springy or MotionPreset.Dynamic => Math.Clamp(options.Springiness + 0.15, 0, 1),
            MotionPreset.Fluid => Math.Clamp(options.Springiness + 0.05, 0, 1),
            _ => options.Springiness
        };
        return BoundsEasingProfile.SoftSpring(springiness);
    }
}
