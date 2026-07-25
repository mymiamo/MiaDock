namespace MiaDock.Core.Settings;

public sealed record StartupShutdownSettings(
    bool StartWithWindows,
    StartupLaunchMode LaunchMode,
    CloseBehaviorSetting CloseBehavior,
    bool HasConfirmedCloseBehavior)
{
    public static StartupShutdownSettings Default { get; } = new(
        false,
        StartupLaunchMode.Island,
        CloseBehaviorSetting.MinimizeToTray,
        false);
}
