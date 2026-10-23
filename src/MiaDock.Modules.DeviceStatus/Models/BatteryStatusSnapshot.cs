namespace MiaDock.Modules.DeviceStatus.Models;

public sealed record BatteryStatusSnapshot(
    DeviceServiceState State,
    bool IsBatteryPresent,
    int ChargePercent,
    bool IsCharging,
    bool IsEnergySaverOn,
    string PowerSource,
    BatteryAvailabilityState Availability = BatteryAvailabilityState.Unknown,
    DateTimeOffset? LastSuccessfulReadAtUtc = null)
{
    public bool HasReading => IsBatteryPresent && Availability is BatteryAvailabilityState.Available or BatteryAvailabilityState.TransientError;
    public static BatteryStatusSnapshot Default { get; } = new(
        DeviceServiceState.Stopped,
        false,
        0,
        false,
        false,
        "Bilinmiyor",
        BatteryAvailabilityState.Unknown,
        null);
}
