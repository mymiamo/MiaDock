namespace MiaDock.Modules.DeviceStatus.Settings;

public interface IBatteryModuleSettings
{
    BatteryModuleOptions Current { get; }
    event EventHandler<BatteryModuleOptions>? Changed;
}
