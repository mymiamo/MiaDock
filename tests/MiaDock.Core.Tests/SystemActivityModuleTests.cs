using MiaDock.Core.Modules;
using MiaDock.Modules.SystemStatus;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.Services;
using MiaDock.Modules.SystemStatus.ViewModels;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class SystemActivityModuleTests
{
    [TestMethod]
    public async Task Activation_ExposesNonPersistentSystemPresentation()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var viewModel = new SystemActivityViewModel(service);
        var module = new SystemActivityModule(service, viewModel);

        await module.ActivateAsync();

        Assert.IsFalse(module.Descriptor.IsPersistent);
        Assert.AreEqual("system-activity", module.CurrentPresentation?.ModuleId);
        Assert.AreEqual(ModulePresentationKind.Status, module.CurrentPresentation?.PresentationKind);
        Assert.AreEqual(string.Empty, module.CurrentPresentation?.ValueText);
        Assert.AreEqual("\uE717", module.Descriptor.IconGlyph);
        Assert.AreEqual(280, module.Descriptor.MinimumExpandedHeight);
        Assert.IsFalse(module.CurrentPresentation?.IsPersistentOverride);
    }

    [TestMethod]
    public async Task MasterVolumeChange_DoesNotDuplicateDedicatedVolumeEvent()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var module = new SystemActivityModule(service, new SystemActivityViewModel(service));
        await module.ActivateAsync();
        ModuleEvent? raised = null;
        module.EventOccurred += (_, moduleEvent) => raised = moduleEvent;

        service.Publish(CreateSnapshot() with { MasterVolume = 0.75 });

        Assert.IsNull(raised);
    }

    [TestMethod]
    public async Task CallActivity_RaisesHighPriorityEvent()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var module = new SystemActivityModule(service, new SystemActivityViewModel(service));
        await module.ActivateAsync();
        ModuleEvent? raised = null;
        module.EventOccurred += (_, moduleEvent) => raised = moduleEvent;

        service.Publish(CreateSnapshot() with
        {
            MicrophoneUsage = MicrophoneUsageState.Active,
            CallActivity = CallActivityState.Possible
        });

        Assert.IsNotNull(raised);
        Assert.AreEqual(ModuleEventPriority.High, raised.Priority);
        Assert.AreEqual("system:call", raised.CoalescingKey);
        Assert.IsFalse(raised.Presentation.IsSensitive);
        Assert.IsTrue(module.CurrentPresentation?.IsPersistentOverride);
        Assert.AreEqual(450, module.CurrentPresentation?.PersistentPriorityOverride);
    }

    [TestMethod]
    public async Task MicrophoneOnlyChange_DoesNotRaisePrivacyEventFromSystemModule()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var module = new SystemActivityModule(service, new SystemActivityViewModel(service));
        await module.ActivateAsync();
        ModuleEvent? raised = null;
        module.EventOccurred += (_, moduleEvent) => raised = moduleEvent;

        service.Publish(CreateSnapshot() with { MicrophoneUsage = MicrophoneUsageState.Active });

        Assert.IsNull(raised);
        Assert.IsFalse(module.CurrentPresentation?.IsPersistentOverride);
    }

    [TestMethod]
    public async Task ApplicationVolumeChange_DoesNotLeakIntoCallModule()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var module = new SystemActivityModule(service, new SystemActivityViewModel(service));
        await module.ActivateAsync();
        ModuleEvent? raised = null;
        module.EventOccurred += (_, moduleEvent) => raised = moduleEvent;

        service.Publish(CreateSnapshot() with { ApplicationVolume = 0.25 });

        Assert.IsNull(raised);
    }

    [TestMethod]
    public async Task ApplicationMute_IsDisabledWhenSessionCannotBeMatched()
    {
        var service = new FakeSystemActivityService(CreateSnapshot() with
        {
            ApplicationVolumeAvailability = ApplicationVolumeAvailability.SessionNotFound
        });
        var module = new SystemActivityModule(service, new SystemActivityViewModel(service));
        await module.ActivateAsync();

        Assert.IsFalse(module.CanExecuteCommand("app-mute"));
        Assert.IsFalse(module.CanExecuteCommand("master-mute"));
        Assert.IsFalse(await module.ExecuteCommandAsync("app-mute"));
        Assert.IsEmpty(module.Descriptor.InteractionCommands);
    }

    [TestMethod]
    public void ViewModel_DisposeUnsubscribesFromService()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        var viewModel = new SystemActivityViewModel(service);

        Assert.AreEqual(1, service.SubscriberCount);

        viewModel.Dispose();

        Assert.AreEqual(0, service.SubscriberCount);
    }

    [TestMethod]
    public void Module_DisposeUnsubscribesFromService()
    {
        var service = new FakeSystemActivityService(CreateSnapshot());
        using var viewModel = new SystemActivityViewModel(service);
        var module = new SystemActivityModule(service, viewModel);

        Assert.AreEqual(2, service.SubscriberCount);

        module.Dispose();

        Assert.AreEqual(1, service.SubscriberCount);
    }

    private static SystemActivitySnapshot CreateSnapshot() => new(
        SystemActivityServiceState.Ready,
        true,
        0.4,
        false,
        ApplicationVolumeAvailability.Available,
        0.6,
        false,
        MicrophoneUsageState.Idle,
        CameraDeviceAvailability.Available,
        CameraAccessState.Allowed,
        CallActivityState.None);

    private sealed class FakeSystemActivityService(SystemActivitySnapshot current) : ISystemActivityService
    {
        private EventHandler<SystemActivitySnapshot>? _snapshotChanged;

        public SystemActivitySnapshot Current { get; private set; } = current;
        public int SubscriberCount { get; private set; }
        public event EventHandler<SystemActivitySnapshot>? SnapshotChanged
        {
            add
            {
                _snapshotChanged += value;
                SubscriberCount++;
            }
            remove
            {
                _snapshotChanged -= value;
                SubscriberCount--;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> SetMasterVolumeAsync(double volume, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> ToggleMasterMuteAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<bool> SetApplicationVolumeAsync(double volume, CancellationToken cancellationToken = default) =>
            Task.FromResult(Current.ApplicationVolumeAvailability == ApplicationVolumeAvailability.Available);
        public Task<bool> ToggleApplicationMuteAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Current.ApplicationVolumeAvailability == ApplicationVolumeAvailability.Available);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Publish(SystemActivitySnapshot snapshot)
        {
            Current = snapshot;
            _snapshotChanged?.Invoke(this, snapshot);
        }
    }
}
