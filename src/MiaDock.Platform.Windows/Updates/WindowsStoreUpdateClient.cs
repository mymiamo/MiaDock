using System.Reflection;
using Windows.ApplicationModel;
using Windows.Networking.Connectivity;
using Windows.Services.Store;
using Windows.System;

namespace MiaDock.Platform.Windows.Updates;

internal sealed class WindowsStoreUpdateClient : IStoreUpdateClient
{
    internal static readonly string StoreProductUri =
        "ms-windows-store://pdp/?ProductId=9PML784D0FDK";

    public bool HasPackageIdentity
    {
        get
        {
            try
            {
                return !string.IsNullOrWhiteSpace(Package.Current.Id.Name);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public bool HasInternetAccess
    {
        get
        {
            try
            {
                return NetworkInformation.GetInternetConnectionProfile()
                    ?.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

    public Version CurrentVersion
    {
        get
        {
            try
            {
                return ToVersion(Package.Current.Id.Version);
            }
            catch (Exception)
            {
                return Normalize(
                    Assembly.GetEntryAssembly()?.GetName().Version ??
                    new Version(1, 1, 0, 0));
            }
        }
    }

    public async Task<IReadOnlyList<Version>> GetAvailableVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        var currentPackageName = Package.Current.Id.Name;
        var updates = await StoreContext.GetDefault()
            .GetAppAndOptionalStorePackageUpdatesAsync()
            .AsTask(cancellationToken)
            .ConfigureAwait(false);
        return updates
            .Where(update => string.Equals(
                update.Package.Id.Name,
                currentPackageName,
                StringComparison.Ordinal))
            .Select(update => ToVersion(update.Package.Id.Version))
            .ToArray();
    }

    public Task<bool> OpenStorePageAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Launcher.LaunchUriAsync(new Uri(StoreProductUri))
            .AsTask(cancellationToken);
    }

    private static Version ToVersion(PackageVersion version) =>
        new(version.Major, version.Minor, version.Build, version.Revision);

    private static Version Normalize(Version version) =>
        new(
            Math.Max(0, version.Major),
            Math.Max(0, version.Minor),
            Math.Max(0, version.Build),
            Math.Max(0, version.Revision));
}
