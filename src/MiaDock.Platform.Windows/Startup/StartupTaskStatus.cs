namespace MiaDock.Platform.Windows.Startup;

public enum StartupTaskStatus
{
    Unavailable,
    Failed,
    Disabled,
    DisabledByUser,
    DisabledByPolicy,
    Enabled,
    EnabledByPolicy
}
