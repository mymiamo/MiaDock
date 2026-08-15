namespace MiaDock.Modules.DeviceStatus.Models;

public sealed record BluetoothDeviceState(
    string Id,
    string DisplayName,
    bool IsConnected,
    bool IsPresent,
    int? BatteryPercentage = null,
    DeviceHubDeviceType DeviceType = DeviceHubDeviceType.GenericBluetoothDevice);
