namespace MiaDock.Platform.Windows.Applications;

internal interface IWindowsUriLauncherClient
{
    Task<bool> LaunchAsync(
        Uri uri,
        CancellationToken cancellationToken = default);
}
