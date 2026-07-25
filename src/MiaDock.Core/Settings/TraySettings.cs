namespace MiaDock.Core.Settings;

public sealed record TraySettings(bool ShowIcon, bool ShowMediaControls, bool EnableTemporaryNotifications)
{
    public static TraySettings Default { get; } = new(true, true, true);
}
