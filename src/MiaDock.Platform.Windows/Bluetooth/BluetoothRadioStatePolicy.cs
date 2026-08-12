using MiaDock.Modules.DeviceStatus.Models;

namespace MiaDock.Platform.Windows.Bluetooth;

internal static class BluetoothRadioStatePolicy
{
    public static BluetoothStatusSnapshot CreateNonDiscoveringSnapshot(BluetoothRadioState radioState) =>
        radioState switch
        {
            BluetoothRadioState.Off => new(
                DeviceServiceState.Ready,
                false,
                Array.Empty<BluetoothDeviceState>(),
                BluetoothRadioState.Off),
            BluetoothRadioState.Unavailable => new(
                DeviceServiceState.Unavailable,
                false,
                Array.Empty<BluetoothDeviceState>(),
                BluetoothRadioState.Unavailable),
            _ => new(
                DeviceServiceState.Starting,
                false,
                Array.Empty<BluetoothDeviceState>(),
                BluetoothRadioState.Unknown)
        };
}
