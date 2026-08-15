namespace MiaDock.Modules.DeviceStatus.Settings;

public interface IDeviceHubSettings
{
    DeviceHubOptions Current { get; }
    event EventHandler<DeviceHubOptions>? Changed;
}
