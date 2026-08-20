using MiaDock.Core.Modules;

namespace MiaDock.Core.Settings;

public sealed record AudibleNotificationSettings(
    bool IsEnabled,
    bool NetworkOfflineEnabled,
    bool ConnectedWithoutInternetEnabled,
    bool LowBatteryEnabled,
    bool DeviceConnectedEnabled,
    bool DeviceDisconnectedEnabled,
    bool HourlyEnabled,
    string? OutputDeviceId = null,
    int VolumePercent = 100)
{
    public static AudibleNotificationSettings Default { get; } = new(
        true,
        true,
        true,
        true,
        true,
        true,
        true);

    public bool Allows(AudibleNotificationCue cue) => IsEnabled && cue switch
    {
        AudibleNotificationCue.NetworkOffline => NetworkOfflineEnabled,
        AudibleNotificationCue.ConnectedWithoutInternet => ConnectedWithoutInternetEnabled,
        AudibleNotificationCue.LowBattery => LowBatteryEnabled,
        AudibleNotificationCue.DeviceConnected => DeviceConnectedEnabled,
        AudibleNotificationCue.DeviceDisconnected => DeviceDisconnectedEnabled,
        AudibleNotificationCue.Hourly => HourlyEnabled,
        _ => false
    };
}
