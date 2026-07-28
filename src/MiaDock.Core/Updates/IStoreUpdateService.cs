namespace MiaDock.Core.Updates;

public interface IStoreUpdateService
{
    StoreUpdateSnapshot Current { get; }

    event EventHandler<StoreUpdateSnapshot>? UpdateAvailabilityChanged;

    Task<StoreUpdateSnapshot> CheckAsync(CancellationToken cancellationToken = default);

    Task<bool> OpenStorePageAsync(CancellationToken cancellationToken = default);
}
