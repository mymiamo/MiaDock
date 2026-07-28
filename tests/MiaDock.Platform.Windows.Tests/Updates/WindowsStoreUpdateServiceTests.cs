using MiaDock.Core.Updates;
using MiaDock.Platform.Windows.Updates;

namespace MiaDock.Platform.Windows.Tests.Updates;

[TestClass]
public sealed class WindowsStoreUpdateServiceTests
{
    [TestMethod]
    public void StoreProductUri_UsesAssignedMicrosoftStoreId()
    {
        Assert.AreEqual(
            "ms-windows-store://pdp/?ProductId=9PML784D0FDK",
            WindowsStoreUpdateClient.StoreProductUri);
    }

    [TestMethod]
    public async Task CheckAsync_WithoutPackageIdentity_DoesNotQueryStore()
    {
        var client = new FakeClient { HasPackageIdentity = false };
        var service = new WindowsStoreUpdateService(client);

        var result = await service.CheckAsync();

        Assert.AreEqual(StoreUpdateStatus.Unavailable, result.Status);
        Assert.AreEqual(0, client.QueryCount);
    }

    [TestMethod]
    public async Task CheckAsync_Offline_DoesNotQueryStore()
    {
        var client = new FakeClient
        {
            HasPackageIdentity = true,
            HasInternetAccess = false
        };
        var service = new WindowsStoreUpdateService(client);

        var result = await service.CheckAsync();

        Assert.AreEqual(StoreUpdateStatus.Offline, result.Status);
        Assert.AreEqual(0, client.QueryCount);
        Assert.IsNotNull(result.CheckedAtUtc);
    }

    [TestMethod]
    public async Task CheckAsync_SelectsNewestVersionAboveCurrent()
    {
        var client = new FakeClient
        {
            HasPackageIdentity = true,
            HasInternetAccess = true,
            CurrentVersion = new Version(1, 1, 0, 0),
            AvailableVersions =
            [
                new Version(1, 1, 0, 0),
                new Version(1, 2, 0, 0),
                new Version(1, 1, 5, 0)
            ]
        };
        var service = new WindowsStoreUpdateService(client);
        var statuses = new List<StoreUpdateStatus>();
        service.UpdateAvailabilityChanged += (_, update) =>
            statuses.Add(update.Status);

        var result = await service.CheckAsync();

        Assert.AreEqual(StoreUpdateStatus.UpdateAvailable, result.Status);
        Assert.AreEqual(new Version(1, 2, 0, 0), result.AvailableVersion);
        CollectionAssert.AreEqual(
            new[] { StoreUpdateStatus.Checking, StoreUpdateStatus.UpdateAvailable },
            statuses);
    }

    [TestMethod]
    public async Task CheckAsync_WhenNoNewerVersion_IsUpToDate()
    {
        var client = new FakeClient
        {
            HasPackageIdentity = true,
            HasInternetAccess = true,
            CurrentVersion = new Version(1, 1, 0, 0),
            AvailableVersions =
            [
                new Version(1, 0, 9, 0),
                new Version(1, 1, 0, 0)
            ]
        };
        var service = new WindowsStoreUpdateService(client);

        var result = await service.CheckAsync();

        Assert.AreEqual(StoreUpdateStatus.UpToDate, result.Status);
        Assert.IsNull(result.AvailableVersion);
    }

    [TestMethod]
    public async Task CheckAsync_WhenStoreFails_ReportsFailureWithoutThrowing()
    {
        var client = new FakeClient
        {
            HasPackageIdentity = true,
            HasInternetAccess = true,
            QueryFailure = new InvalidOperationException()
        };
        var service = new WindowsStoreUpdateService(client);

        var result = await service.CheckAsync();

        Assert.AreEqual(StoreUpdateStatus.Failed, result.Status);
    }

    [TestMethod]
    public async Task OpenStorePageAsync_DelegatesToProductUriClient()
    {
        var client = new FakeClient { OpenResult = true };
        var service = new WindowsStoreUpdateService(client);

        Assert.IsTrue(await service.OpenStorePageAsync());
        Assert.AreEqual(1, client.OpenCount);
    }

    private sealed class FakeClient : IStoreUpdateClient
    {
        public bool HasPackageIdentity { get; set; }
        public bool HasInternetAccess { get; set; }
        public Version CurrentVersion { get; set; } = new(1, 1, 0, 0);
        public IReadOnlyList<Version> AvailableVersions { get; set; } = [];
        public Exception? QueryFailure { get; set; }
        public bool OpenResult { get; set; }
        public int QueryCount { get; private set; }
        public int OpenCount { get; private set; }

        public Task<IReadOnlyList<Version>> GetAvailableVersionsAsync(
            CancellationToken cancellationToken = default)
        {
            QueryCount++;
            return QueryFailure is null
                ? Task.FromResult(AvailableVersions)
                : Task.FromException<IReadOnlyList<Version>>(QueryFailure);
        }

        public Task<bool> OpenStorePageAsync(
            CancellationToken cancellationToken = default)
        {
            OpenCount++;
            return Task.FromResult(OpenResult);
        }
    }
}
