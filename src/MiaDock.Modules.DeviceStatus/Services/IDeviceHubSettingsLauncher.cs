namespace MiaDock.Modules.DeviceStatus.Services;

public interface IDeviceHubSettingsLauncher
{
    Task<bool> OpenBluetoothSettingsAsync(CancellationToken cancellationToken = default);

    Task<bool> OpenSoundSettingsAsync(CancellationToken cancellationToken = default);
}
