namespace MiaDock.Modules.DeviceStatus.Models;

public sealed record BatteryStatusSnapshot(
    DeviceServiceState State,
    bool IsBatteryPresent,
    int ChargePercent,
    bool IsCharging,
    bool IsEnergySaverOn,
    string PowerSource)
{
    public static BatteryStatusSnapshot Default { get; } = new(
        DeviceServiceState.Stopped,
        false,
        0,
        false,
        false,
        "Bilinmiyor");
}
