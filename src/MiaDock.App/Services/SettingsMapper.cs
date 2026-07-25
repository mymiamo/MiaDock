using MiaDock.Core.Presentation;
using MiaDock.Core.Overlay;
using MiaDock.Core.Settings;
using MiaDock.Modules.Media.Models;

namespace MiaDock.App.Services;

public static class SettingsMapper
{
    public static OverlayPosition ToOverlayPosition(IslandPositionSetting position) => position switch
    {
        IslandPositionSetting.TopCenter => OverlayPosition.TopCenter,
        IslandPositionSetting.TopLeft => OverlayPosition.TopLeft,
        IslandPositionSetting.TopRight => OverlayPosition.TopRight,
        IslandPositionSetting.BottomCenter => OverlayPosition.BottomCenter,
        IslandPositionSetting.BottomLeft => OverlayPosition.BottomLeft,
        IslandPositionSetting.BottomRight => OverlayPosition.BottomRight,
        _ => OverlayPosition.TopCenter
    };

    public static IslandLayoutOptions ToLayoutOptions(AppearanceSettings settings) => new(
        settings.CollapsedWidth,
        settings.CollapsedHeight,
        settings.HoverWidth,
        settings.HoverHeight,
        settings.ExpandedWidth,
        settings.ExpandedHeight,
        settings.NotificationWidth,
        settings.NotificationHeight,
        settings.CornerRadius);

    public static IslandMotionOptions ToMotionOptions(MiaDockSettings settings)
    {
        var speed = settings.Appearance.AnimationSpeed;
        var defaults = IslandMotionOptions.Default;
        return defaults with
        {
            HoverDuration = Divide(defaults.HoverDuration, speed),
            ExpandDuration = Divide(defaults.ExpandDuration, speed),
            CollapseDuration = Divide(defaults.CollapseDuration, speed),
            NotificationEnterDuration = Divide(defaults.NotificationEnterDuration, speed),
            NotificationExitDuration = Divide(defaults.NotificationExitDuration, speed),
            ContentRefreshDuration = Divide(defaults.ContentRefreshDuration, speed),
            NotificationVisibleDuration = TimeSpan.FromSeconds(settings.Fullscreen.NotificationSeconds),
            AnimationKind = settings.Appearance.AnimationKind
        };
    }

    public static MediaSelectionOptions ToMediaSelection(MediaSettings settings) => new(
        settings.SelectedSourceId,
        settings.Fallback == MediaFallbackSetting.SelectedOnly
            ? MediaFallbackBehavior.SelectedSourceOnly
            : MediaFallbackBehavior.UseAnotherActiveSession);

    private static TimeSpan Divide(TimeSpan duration, double divisor) =>
        TimeSpan.FromTicks((long)(duration.Ticks / divisor));
}
