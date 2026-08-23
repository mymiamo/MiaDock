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
                ResolveCornerRadii(radii, layout.CollapsedHeight, layout.CornerStyle)),
            IslandVisualState.Hover => new(
                layout.HoverWidth,
                layout.HoverHeight,
                ResolveCornerRadii(radii, layout.HoverHeight, layout.CornerStyle)),
            IslandVisualState.ExpandedModule => new(
                layout.ExpandedWidth,
                layout.ExpandedHeight,
                ResolveCornerRadii(radii, layout.ExpandedHeight, layout.CornerStyle)),
            IslandVisualState.ModuleNotification => new(
                layout.NotificationWidth,
                layout.NotificationHeight,
                ResolveCornerRadii(radii, layout.NotificationHeight, layout.CornerStyle)),
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
        if (!IsEventMorph(transition) || !options.EnableEventMorph || options.Preset == MotionPreset.Off)
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

    private static DockCornerRadii ResolveCornerRadii(
        DockCornerRadii radii,
        double height,
        DockCornerStyle style)
    {
        var maximum = height / 2;
        return style switch
        {
            DockCornerStyle.Soft => radii.Clamp(0, Math.Min(18, maximum)),
            DockCornerStyle.Rounded => DockCornerRadii.Uniform(maximum),
            _ => radii.Clamp(0, maximum)
        };
    }
}
