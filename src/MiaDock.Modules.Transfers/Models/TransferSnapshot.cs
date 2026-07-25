namespace MiaDock.Modules.Transfers.Models;

public sealed record TransferSnapshot(
    string ProviderId,
    string TransferId,
    string SafeDisplayName,
    long TransferredBytes,
    long TotalBytes,
    TransferStatus Status,
    DateTimeOffset LastUpdatedUtc)
{
    public double? Progress => TotalBytes > 0
        ? Math.Clamp((double)TransferredBytes / TotalBytes, 0, 1)
        : null;

    public bool IsTerminal => Status is TransferStatus.Completed or TransferStatus.Failed or
        TransferStatus.Cancelled or TransferStatus.Disconnected;
}
