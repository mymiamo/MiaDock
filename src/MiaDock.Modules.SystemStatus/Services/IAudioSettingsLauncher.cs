namespace MiaDock.Modules.SystemStatus.Services;

public interface IAudioSettingsLauncher
{
    Task<bool> OpenSoundSettingsAsync(CancellationToken cancellationToken = default);
}
