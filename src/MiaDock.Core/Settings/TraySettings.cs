namespace MiaDock.Core.Settings;

public sealed record TraySettings(
    bool ShowIcon,
    bool ShowMediaControls,
    bool EnableTemporaryNotifications,
    TrayPrimaryAction PrimaryAction = TrayPrimaryAction.OpenSettings)
{
    public static TraySettings Default { get; } =
        new(true, true, true, TrayPrimaryAction.OpenSettings);
}
