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
        var motion = settings.Appearance.Motion ?? MotionSettings.FromLegacy(
            settings.Appearance.AnimationKind,
            settings.Appearance.AnimationSpeed);
        var speed = motion.Speed;
        var defaults = IslandMotionOptions.Default;
        var profileScale = motion.Preset switch
        {
            MotionPreset.Off => 0,
            MotionPreset.Minimal => 0.7,
            MotionPreset.Fluid => 1.15,
            MotionPreset.Springy => 1.05,
            MotionPreset.Dynamic => 1.2,
            _ => 1
        };
        return defaults with
        {
            HoverDuration = Scale(defaults.HoverDuration, profileScale, speed),
            ExpandDuration = Scale(defaults.ExpandDuration, profileScale, speed),
            CollapseDuration = Scale(defaults.CollapseDuration, profileScale, speed),
            NotificationEnterDuration = Scale(defaults.NotificationEnterDuration, profileScale, speed),
            NotificationExitDuration = Scale(defaults.NotificationExitDuration, profileScale, speed),
            ContentRefreshDuration = Scale(defaults.ContentRefreshDuration, profileScale, speed),
            NotificationVisibleDuration = TimeSpan.FromSeconds(settings.Fullscreen.NotificationSeconds),
            Preset = motion.Preset,
            Intensity = motion.Intensity,
            Springiness = motion.Springiness,
            ContentDelay = TimeSpan.FromMilliseconds(motion.ContentDelayMilliseconds),
            EnableParallax = motion.EnableParallax,
            EnableTransientBlur = motion.EnableTransientBlur
        };
    }

    public static MediaSelectionOptions ToMediaSelection(MediaSettings settings) => new(
        settings.SelectedSourceId,
        settings.Fallback == MediaFallbackSetting.SelectedOnly
            ? MediaFallbackBehavior.SelectedSourceOnly
            : MediaFallbackBehavior.UseAnotherActiveSession);

    private static TimeSpan Divide(TimeSpan duration, double divisor) =>
        TimeSpan.FromTicks((long)(duration.Ticks / divisor));

    private static TimeSpan Scale(TimeSpan duration, double scale, double speed) =>
        scale <= 0 ? TimeSpan.Zero : Divide(TimeSpan.FromTicks((long)(duration.Ticks * scale)), speed);
}
