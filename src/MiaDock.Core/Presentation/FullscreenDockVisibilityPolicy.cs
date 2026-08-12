using MiaDock.Core.Settings;

namespace MiaDock.Core.Presentation;

public sealed record FullscreenDockVisibilityContext(
    FullscreenDockBehavior Behavior,
    bool FullscreenAffectsDockDisplay,
    bool ManuallyHidden,
    bool NormalVisibilityAllowed,
    bool NotificationVisible,
    bool NotificationAllowed,
    bool HoverRevealActive,
    bool InteractionActive,
    bool PointerPressed,
    bool Expanded);

public sealed record FullscreenDockVisibilityDecision(
    bool ShowWindow,
    bool HideAtEdge,
    bool FullscreenPolicyApplied);

public static class FullscreenDockVisibilityPolicy
{
    public static FullscreenDockVisibilityDecision Evaluate(
        FullscreenDockVisibilityContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var notification = context.NotificationVisible && context.NotificationAllowed;
        var normal = !context.ManuallyHidden && context.NormalVisibilityAllowed;
        if (!context.FullscreenAffectsDockDisplay)
        {
            return new(normal || notification, false, false);
        }

        var interactionHold = context.InteractionActive || context.PointerPressed;
        return context.Behavior switch
        {
            FullscreenDockBehavior.HideCompletely =>
                new(interactionHold, false, true),
            FullscreenDockBehavior.NotificationsOnly =>
                new(notification || interactionHold, false, true),
            FullscreenDockBehavior.EdgeReveal => EvaluateEdgeReveal(
                context,
                notification,
                interactionHold),
            FullscreenDockBehavior.KeepVisible =>
                new(normal || notification || interactionHold, false, true),
            _ => new(notification || interactionHold, false, true)
        };
    }

    private static FullscreenDockVisibilityDecision EvaluateEdgeReveal(
        FullscreenDockVisibilityContext context,
        bool notification,
        bool interactionHold)
    {
        var revealed = notification ||
                       context.HoverRevealActive ||
                       interactionHold ||
                       context.Expanded;
        return new(true, !revealed, true);
    }
}
