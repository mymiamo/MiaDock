using MiaDock.Modules.SystemStatus.Services;
using Windows.System;

namespace MiaDock.Platform.Windows.Audio;

public sealed class WindowsAudioSettingsLauncher : IAudioSettingsLauncher
{
    private static readonly Uri SoundSettingsUri = new("ms-settings:sound");

    public async Task<bool> OpenSoundSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await Launcher.LaunchUriAsync(SoundSettingsUri);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }
}
