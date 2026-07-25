namespace MiaDock.Modules.Transfers.Settings;

public interface ITransferModuleSettings
{
    TransferModuleOptions Current { get; }

    event EventHandler<TransferModuleOptions>? Changed;
}
