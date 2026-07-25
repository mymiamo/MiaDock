using MiaDock.Modules.Transfers.Models;

namespace MiaDock.Modules.Transfers.Services;

public interface ITransferProgressProvider : IAsyncDisposable
{
    TransferProviderState State { get; }

    event EventHandler<TransferProgressMessage>? MessageReceived;

    event EventHandler<TransferProviderState>? StateChanged;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
