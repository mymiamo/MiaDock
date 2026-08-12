namespace MiaDock.Core.Applications;

public interface IExternalUriLauncher
{
    Task<bool> LaunchAsync(
        Uri uri,
        CancellationToken cancellationToken = default);
}
