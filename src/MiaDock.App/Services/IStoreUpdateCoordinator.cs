using MiaDock.Core.Updates;

namespace MiaDock.App.Services;

public interface IStoreUpdateCoordinator : IAsyncDisposable
{
    StoreUpdateSnapshot Current { get; }

    bool AutomaticChecksEnabled { get; }

    event EventHandler<StoreUpdateSnapshot>? UpdateAvailabilityChanged;

    void Start();

    Task<StoreUpdateSnapshot> CheckNowAsync(
        CancellationToken cancellationToken = default);

    Task<bool> OpenStorePageAsync(
        CancellationToken cancellationToken = default);

    void SetAutomaticChecksEnabled(bool enabled);
}
