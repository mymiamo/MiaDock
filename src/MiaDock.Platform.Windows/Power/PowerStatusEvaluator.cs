using MiaDock.Modules.DeviceStatus.Models;

namespace MiaDock.Platform.Windows.Power;

internal sealed record PowerStatusReading(
    string BatteryStatus,
    string PowerSupplyStatus,
    string PowerSourceKind,
    int RemainingChargePercent,
    bool IsEnergySaverOn);

internal static class PowerStatusEvaluator
{
    public static BatteryStatusSnapshot Evaluate(
        PowerStatusReading reading,
        DateTimeOffset readAtUtc)
    {
        ArgumentNullException.ThrowIfNull(reading);
        var batteryStatus = reading.BatteryStatus.Trim();
        var source = NormalizeSource(reading.PowerSourceKind);
        var isNotPresent = batteryStatus.Equals("NotPresent", StringComparison.OrdinalIgnoreCase);
        var hasBatterySignal = batteryStatus.Equals("Charging", StringComparison.OrdinalIgnoreCase) ||
                               batteryStatus.Equals("Discharging", StringComparison.OrdinalIgnoreCase) ||
                               batteryStatus.Equals("Idle", StringComparison.OrdinalIgnoreCase) ||
                               source == "DC";
        var availability = isNotPresent
            ? BatteryAvailabilityState.NotPresent
            : hasBatterySignal
                ? BatteryAvailabilityState.Available
                : BatteryAvailabilityState.Unknown;
        var present = availability == BatteryAvailabilityState.Available;

        return new BatteryStatusSnapshot(
            DeviceServiceState.Ready,
            present,
            present ? Math.Clamp(reading.RemainingChargePercent, 0, 100) : 0,
            present && batteryStatus.Equals("Charging", StringComparison.OrdinalIgnoreCase),
            reading.IsEnergySaverOn,
            source,
            availability,
            readAtUtc.ToUniversalTime());
    }

    private static string NormalizeSource(string value) => value.Trim() switch
    {
        "AC" => "AC",
        "Battery" => "DC",
        "USB" => "USB",
        "Wireless" => "Wireless",
        _ => "Unknown"
    };
}

internal interface IWindowsPowerStatusReader
{
    PowerStatusReading Read();
}

internal interface IWindowsPowerEventSource
{
    void Subscribe(EventHandler<object> handler);

    void Unsubscribe(EventHandler<object> handler);
}
