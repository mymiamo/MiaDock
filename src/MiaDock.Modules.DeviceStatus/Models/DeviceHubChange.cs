namespace MiaDock.Modules.DeviceStatus.Models;

public enum DeviceHubChangeKind
{
    Connected,
    Disconnected,
    BatteryLow,
    DefaultAudioOutputChanged,
    SafeToRemove
}

public sealed record DeviceHubChange(
    DeviceHubChangeKind Kind,
    DeviceHubDevice Device,
    DeviceHubDevice? PreviousDevice = null);
