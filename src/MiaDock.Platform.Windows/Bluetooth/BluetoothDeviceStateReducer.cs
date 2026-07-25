using MiaDock.Modules.DeviceStatus.Models;

namespace MiaDock.Platform.Windows.Bluetooth;

public static class BluetoothDeviceStateReducer
{
    public static IReadOnlyList<BluetoothDeviceState> Merge(IEnumerable<BluetoothDeviceState> devices) => devices
        .GroupBy(device => device.Id, StringComparer.Ordinal)
        .Select(group => new BluetoothDeviceState(
            group.Key,
            group.Select(device => device.DisplayName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "Bluetooth cihazı",
            group.Any(device => device.IsConnected),
            group.Any(device => device.IsPresent)))
        .OrderBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();
}
