using MiaDock.Modules.SystemStatus.Services;
using Windows.System;

namespace MiaDock.Platform.Windows.Settings;

public sealed class WindowsPrivacySettingsLauncher : IPrivacySettingsLauncher
{
    public Task<bool> OpenMicrophonePrivacySettingsAsync(CancellationToken cancellationToken = default) =>
        LaunchAsync("ms-settings:privacy-microphone", cancellationToken);

    public Task<bool> OpenCameraPrivacySettingsAsync(CancellationToken cancellationToken = default) =>
        LaunchAsync("ms-settings:privacy-webcam", cancellationToken);

    private static async Task<bool> LaunchAsync(string uri, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await Launcher.LaunchUriAsync(new Uri(uri)).AsTask(cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
