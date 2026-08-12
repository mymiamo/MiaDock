using Windows.System;

namespace MiaDock.Platform.Windows.Applications;

internal sealed class WindowsUriLauncherClient : IWindowsUriLauncherClient
{
    public async Task<bool> LaunchAsync(
        Uri uri,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await Launcher.LaunchUriAsync(uri);
    }
}
