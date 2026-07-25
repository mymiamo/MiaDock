namespace MiaDock.Modules.Transfers.Models;

public sealed record TransferProgressMessage(
    int ProtocolVersion,
    string ProviderId,
    string TransferId,
    string SafeDisplayName,
    long TransferredBytes,
    long TotalBytes,
    TransferStatus Status,
    DateTimeOffset TimestampUtc);
