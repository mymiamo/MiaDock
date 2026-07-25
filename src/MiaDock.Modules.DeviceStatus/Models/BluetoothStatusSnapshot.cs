namespace MiaDock.Modules.DeviceStatus.Models;

public sealed record BluetoothStatusSnapshot(
    DeviceServiceState State,
    bool IsEnumerationComplete,
    IReadOnlyList<BluetoothDeviceState> Devices)
{
    public static BluetoothStatusSnapshot Default { get; } = new(
        DeviceServiceState.Stopped,
        false,
        Array.Empty<BluetoothDeviceState>());
}
