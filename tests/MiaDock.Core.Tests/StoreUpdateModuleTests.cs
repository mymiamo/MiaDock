using MiaDock.App.Modules;
using MiaDock.Core.Modules;
using MiaDock.Core.Updates;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class StoreUpdateModuleTests
{
    [TestMethod]
    public async Task AvailableUpdate_PublishesFullscreenIneligibleEventOnceCalled()
    {
        var service = new FakeStoreUpdateService();
        var module = new StoreUpdateModule(service, Localizer());
        await module.ActivateAsync();
        ModuleEvent? published = null;
        module.EventOccurred += (_, value) => published = value;

        module.PublishAvailable(new StoreUpdateSnapshot(
            StoreUpdateStatus.UpdateAvailable,
            new Version(1, 1, 0, 0),
            new Version(1, 2, 0, 0)));

        Assert.IsNotNull(published);
        Assert.AreEqual(ModuleEventKind.UpdateAvailable, published.Kind);
        Assert.IsFalse(published.IsFullscreenEligible);
        Assert.AreEqual("store-update:1.2.0.0", published.CoalescingKey);
        Assert.AreEqual("Yeni sürüm mevcut", published.Presentation.PrimaryText);
        Assert.AreEqual("MiaDock 1.1.0.0 → 1.2.0.0", published.Presentation.SecondaryText);
        Assert.IsTrue(module.CanExecuteCommand(StoreUpdateModule.OpenStoreCommandId));
    }

    [TestMethod]
    public async Task OpenStoreCommand_UsesStoreService()
    {
        var service = new FakeStoreUpdateService { OpenResult = true };
        var module = new StoreUpdateModule(service, Localizer());
        await module.ActivateAsync();

        var result = await module.ExecuteCommandAsync(
            StoreUpdateModule.OpenStoreCommandId);

        Assert.IsTrue(result);
        Assert.AreEqual(1, service.OpenCount);
    }

    [TestMethod]
    public async Task NonAvailableState_DoesNotPublishEvent()
    {
        var module = new StoreUpdateModule(
            new FakeStoreUpdateService(),
            Localizer());
        await module.ActivateAsync();
        var eventCount = 0;
        module.EventOccurred += (_, _) => eventCount++;

        module.PublishAvailable(new StoreUpdateSnapshot(
            StoreUpdateStatus.UpToDate,
            new Version(1, 1, 0, 0)));

        Assert.AreEqual(0, eventCount);
    }

    private static TestLocalizationService Localizer() =>
        new(new Dictionary<string, (string Turkish, string English)>
        {
            ["Update.Available"] = ("Yeni sürüm mevcut", "A new version is available"),
            ["Update.VersionPair"] = ("MiaDock {0} → {1}", "MiaDock {0} → {1}"),
            ["Update.OpenStore"] = ("Microsoft Store'da aç", "Open in Microsoft Store")
        });

    private sealed class FakeStoreUpdateService : IStoreUpdateService
    {
        public StoreUpdateSnapshot Current { get; } =
            StoreUpdateSnapshot.Unavailable(new Version(1, 1, 0, 0));
        public bool OpenResult { get; set; }
        public int OpenCount { get; private set; }
        public event EventHandler<StoreUpdateSnapshot>? UpdateAvailabilityChanged
        {
            add { }
            remove { }
        }

        public Task<StoreUpdateSnapshot> CheckAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Current);

        public Task<bool> OpenStorePageAsync(
            CancellationToken cancellationToken = default)
        {
            OpenCount++;
            return Task.FromResult(OpenResult);
        }
    }
}
