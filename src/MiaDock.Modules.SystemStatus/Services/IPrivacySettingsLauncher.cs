namespace MiaDock.Modules.SystemStatus.Services;

public interface IPrivacySettingsLauncher
{
    Task<bool> OpenMicrophonePrivacySettingsAsync(CancellationToken cancellationToken = default);

    Task<bool> OpenCameraPrivacySettingsAsync(CancellationToken cancellationToken = default);
}
