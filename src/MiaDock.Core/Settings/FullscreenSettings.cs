namespace MiaDock.Core.Settings;

public sealed record FullscreenSettings(
    bool Enabled,
    double NotificationSeconds,
    FullscreenNotificationStyle Style,
    bool ShowTrackChanges)
{
    public static FullscreenSettings Default { get; } = new(true, 5, FullscreenNotificationStyle.Minimal, true);
}
