namespace MiaDock.Modules.Transfers.Models;

public enum TransferStatus
{
    Queued,
    Running,
    Paused,
    Waiting,
    Completed,
    Failed,
    Cancelled,
    Disconnected
}
