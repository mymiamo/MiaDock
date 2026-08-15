using MiaDock.Modules.DeviceStatus.Models;

namespace MiaDock.Platform.Windows.Bluetooth;

public static class BluetoothDeviceStateReducer
{
    public static IReadOnlyList<BluetoothDeviceState> Merge(IEnumerable<BluetoothDeviceState> devices) => devices
        .GroupBy(device => device.Id, StringComparer.Ordinal)
        .Select(MergeGroup)
        .OrderBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    private static BluetoothDeviceState MergeGroup(IGrouping<string, BluetoothDeviceState> group)
    {
        var items = group.ToArray();
        var connected = items.FirstOrDefault(device => device.IsConnected);
        var preferred = connected ?? items[0];
        int? battery = null;
        foreach (var percentage in items.Select(device => device.BatteryPercentage))
        {
            if (percentage is >= 0 and <= 100 && (battery is null || percentage > battery))
                battery = percentage;
        }

        var typed = items.FirstOrDefault(device =>
            device.DeviceType is not DeviceHubDeviceType.GenericBluetoothDevice and not DeviceHubDeviceType.Unknown);
        return new BluetoothDeviceState(
            group.Key,
            items.Select(device => device.DisplayName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                ?? "Bluetooth cihazı",
            items.Any(device => device.IsConnected),
            items.Any(device => device.IsPresent),
            battery,
            typed?.DeviceType ?? DeviceHubDeviceType.GenericBluetoothDevice,
            FirstNonEmpty(connected?.EndpointId, items.Select(device => device.EndpointId)),
            FirstNonEmpty(preferred.DeviceAddress, items.Select(device => device.DeviceAddress)));
    }

    private static string? FirstNonEmpty(string? preferred, IEnumerable<string?> values)
    {
        if (!string.IsNullOrWhiteSpace(preferred)) return preferred;
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
