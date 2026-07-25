namespace MiaDock.Modules.Transfers.Services;

public enum TransferProviderState
{
    Stopped,
    Starting,
    Listening,
    Unavailable,
    Faulted
}
