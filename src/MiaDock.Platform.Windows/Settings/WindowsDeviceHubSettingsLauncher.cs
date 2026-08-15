using MiaDock.Modules.DeviceStatus.Services;
using MiaDock.Modules.SystemStatus.Services;

namespace MiaDock.Platform.Windows.Settings;

public sealed class WindowsDeviceHubSettingsLauncher : IDeviceHubSettingsLauncher
{
    private static readonly Uri BluetoothSettingsUri = new("ms-settings:bluetooth");
    private readonly IAudioSettingsLauncher _audioSettings;

    public WindowsDeviceHubSettingsLauncher(IAudioSettingsLauncher audioSettings) => _audioSettings = audioSettings;

    public Task<bool> OpenSoundSettingsAsync(CancellationToken cancellationToken = default) =>
        _audioSettings.OpenSoundSettingsAsync(cancellationToken);

    public async Task<bool> OpenBluetoothSettingsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { return await global::Windows.System.Launcher.LaunchUriAsync(BluetoothSettingsUri); }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException) { return false; }
    }
}
