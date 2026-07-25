using MiaDock.Modules.Transfers.Models;

namespace MiaDock.Modules.Transfers.Services;

public interface ITransferStateService : IAsyncDisposable
{
    IReadOnlyList<TransferSnapshot> ActiveTransfers { get; }

    event EventHandler<IReadOnlyList<TransferSnapshot>>? TransfersChanged;

    event EventHandler<TransferSnapshot>? SnapshotChanged;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    void EvaluateHeartbeats();
}
