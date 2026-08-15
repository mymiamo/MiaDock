namespace MiaDock.Modules.DeviceStatus.Models;

public enum DeviceHubDeviceCategory
{
    Bluetooth,
    AudioOutput,
    AudioInput,
    RemovableStorage,
    Unknown
}

public enum DeviceHubConnectionState
{
    Unknown,
    Connected,
    Disconnected
}

public enum DeviceHubDeviceType
{
    Unknown,
    Headphones,
    Headset,
    Speaker,
    Mouse,
    Keyboard,
    Gamepad,
    GenericBluetoothDevice
}

[Flags]
public enum DeviceHubDeviceCapabilities
{
    None = 0,
    Open = 1,
    OpenSettings = 2,
    HasBattery = 4,
    ManageInSettings = 8,
    Eject = 16
}

public sealed record DeviceHubDevice(
    string Id,
    string DisplayName,
    DeviceHubDeviceCategory Category,
    DeviceHubConnectionState ConnectionState,
    bool IsDefault,
    int? BatteryPercentage,
    DeviceHubDeviceCapabilities Capabilities,
    string? Detail = null,
    string? NativeDeviceId = null,
    DateTimeOffset? LastChangedAt = null,
    DeviceHubDeviceType DeviceType = DeviceHubDeviceType.Unknown,
    string? FileSystem = null,
    long? TotalSpace = null,
    long? FreeSpace = null,
    bool CanEject = false,
    string? DeviceInstanceId = null)
{
    public bool HasBattery => BatteryPercentage is >= 0 and <= 100;
    public string? BatteryText => HasBattery ? $"{BatteryPercentage}%" : null;
}
