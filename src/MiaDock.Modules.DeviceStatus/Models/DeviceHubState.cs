namespace MiaDock.Modules.DeviceStatus.Models;

public sealed record DeviceHubState(
    DeviceServiceState State,
    bool IsInitialSnapshot,
    IReadOnlyList<DeviceHubDevice> BluetoothDevices,
    IReadOnlyList<DeviceHubDevice> AudioOutputDevices,
    IReadOnlyList<DeviceHubDevice> AudioInputDevices,
    IReadOnlyList<DeviceHubDevice> StorageDevices)
{
    public static DeviceHubState Default { get; } = new(
        DeviceServiceState.Stopped,
        true,
        Array.Empty<DeviceHubDevice>(),
        Array.Empty<DeviceHubDevice>(),
        Array.Empty<DeviceHubDevice>(),
        Array.Empty<DeviceHubDevice>());

    public IReadOnlyList<DeviceHubDevice> BatteryDevices =>
        BluetoothDevices.Where(device => device.HasBattery).ToArray();
}
