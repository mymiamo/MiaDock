namespace MiaDock.Platform.Windows.Updates;

internal interface IStoreUpdateClient
{
    bool HasPackageIdentity { get; }

    bool HasInternetAccess { get; }

    Version CurrentVersion { get; }

    Task<IReadOnlyList<Version>> GetAvailableVersionsAsync(
        CancellationToken cancellationToken = default);

    Task<bool> OpenStorePageAsync(CancellationToken cancellationToken = default);
}
