using MiaDock.Core.Modules;
using MiaDock.Modules.Transfers;
using MiaDock.Modules.Transfers.Models;
using MiaDock.Modules.Transfers.Services;
using MiaDock.Modules.Transfers.Settings;
using MiaDock.Modules.Transfers.ViewModels;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class TransferModuleTests
{
    [TestMethod]
    public async Task RunningTransfer_IsPersistentAndCompletionCreatesElevatedEvent()
    {
        var provider = new TransferStateServiceTests.FakeTransferProvider();
        await using var service = new TransferStateService(provider);
        using var viewModel = new TransferModuleViewModel(service);
        using var module = new TransferModule(
            service,
            viewModel,
            new FakeSettings(new TransferModuleOptions(true, TimeSpan.FromSeconds(7), false)));
        ModuleEvent? received = null;
        module.EventOccurred += (_, moduleEvent) => received = moduleEvent;
        await module.ActivateAsync();

        provider.Publish(CreateMessage(TransferStatus.Running, 40));
        Assert.IsNotNull(module.CurrentPresentation);
        Assert.IsTrue(module.CurrentPresentation.IsPersistentOverride);
        Assert.AreEqual(0.4, module.CurrentPresentation.Progress);

        provider.Publish(CreateMessage(TransferStatus.Completed, 100));
        Assert.IsNotNull(received);
        Assert.AreEqual(ModuleEventPriority.Elevated, received.Priority);
        Assert.AreEqual(TimeSpan.FromSeconds(7), received.DisplayDuration);
        Assert.IsFalse(received.IsFullscreenEligible);
        Assert.IsNull(module.CurrentPresentation);
    }

    private static TransferProgressMessage CreateMessage(TransferStatus status, long transferred) => new(
        TransferProtocol.CurrentVersion,
        "test.provider",
        "transfer-1",
        "Test aktarımı",
        transferred,
        100,
        status,
        DateTimeOffset.UtcNow);

    private sealed class FakeSettings(TransferModuleOptions options) : ITransferModuleSettings
    {
        public TransferModuleOptions Current { get; } = options;
        public event EventHandler<TransferModuleOptions>? Changed { add { } remove { } }
    }
}
