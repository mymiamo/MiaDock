using MiaDock.Modules.SystemStatus.Models;

namespace MiaDock.Modules.SystemStatus.Services;

public interface IPrivacyUsageService : IAsyncDisposable
{
    PrivacyState Current { get; }

    event EventHandler<PrivacyState>? StateChanged;

    Task StartAsync(CancellationToken cancellationToken = default);
}
