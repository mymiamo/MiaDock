using MiaDock.Core.Applications;
using MiaDock.Core.Logging;

namespace MiaDock.Platform.Windows.Applications;

public sealed class WindowsExternalUriLauncher : IExternalUriLauncher
{
    private readonly IWindowsUriLauncherClient _client;
    private readonly ILogService? _log;

    public WindowsExternalUriLauncher(ILogService log)
        : this(new WindowsUriLauncherClient(), log)
    {
    }

    internal WindowsExternalUriLauncher(
        IWindowsUriLauncherClient client,
        ILogService? log = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _log = log;
    }

    public async Task<bool> LaunchAsync(
        Uri uri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!uri.IsAbsoluteUri ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            LogFailure(uri, exception: null, "invalid-uri");
            return false;
        }

        try
        {
            return await _client
                .LaunchAsync(uri, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogFailure(uri, exception, "launch");
            return false;
        }
    }

    private void LogFailure(Uri uri, Exception? exception, string operation) =>
        _log?.Write(
            TechnicalLogLevel.Warning,
            TechnicalEventIds.ExternalUriLaunchFailed,
            "ExternalLinks",
            "An external URI could not be opened.",
            exception,
            new Dictionary<string, object?>
            {
                ["host"] = uri.IsAbsoluteUri ? uri.Host : null,
                ["operation"] = operation
            });
}
