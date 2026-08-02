using MiaDock.Core.Focus;
using Windows.System;

namespace MiaDock.Platform.Windows.Applications;

public sealed class WindowsFocusSettingsLauncher : IFocusSettingsLauncher
{
    private static readonly Uri FocusSettingsUri = new("ms-settings:quiethours");

    public async Task<bool> OpenWindowsFocusSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await Launcher.LaunchUriAsync(FocusSettingsUri);
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }
}
