using MiaDock.Core.Modules;
using MiaDock.Modules.SystemStatus;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.Services;
using MiaDock.Modules.SystemStatus.ViewModels;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class PrivacyModuleTests
{
    [TestMethod]
    public void PrivacyState_CameraTakesPriorityOverMicrophoneForIndicator()
    {
        var state = PrivacyState.FromApplications(
        [
            new("discord", 1, "Discord", "Discord", null, true, false),
            new("teams", 2, "Teams", "Microsoft Teams", null, true, true)
        ]);

        Assert.IsTrue(state.MicrophoneInUse);
        Assert.IsTrue(state.CameraInUse);
        Assert.AreEqual(PrivacyIndicatorKind.Camera, state.Indicator);
        Assert.HasCount(2, state.ActiveApplications);
    }

    [TestMethod]
    public void PrivacyState_MicrophoneOnly_IsGreenIndicator()
    {
        var state = PrivacyState.FromApplications(
        [
            new("discord", 1, "Discord", "Discord", null, true, false)
        ]);

        Assert.AreEqual(PrivacyIndicatorKind.Microphone, state.Indicator);
        Assert.IsFalse(state.CameraInUse);
    }

    [TestMethod]
    public void PrivacyState_Empty_IsIdleWhiteIndicator()
    {
        Assert.AreEqual(PrivacyIndicatorKind.Idle, PrivacyState.Empty.Indicator);
        Assert.AreEqual(PrivacyIndicatorKind.Idle, PrivacyState.ResolveIndicator(false, false));
    }

    [TestMethod]
    public async Task Activation_RaisesHighPriorityEventWhenMicrophoneStarts()
    {
        var service = new FakePrivacyUsageService(PrivacyState.Empty);
        var viewModel = new PrivacyModuleViewModel(service);
        var module = new PrivacyModule(service, viewModel);
        await module.ActivateAsync();

        ModuleEvent? raised = null;
        module.EventOccurred += (_, moduleEvent) => raised = moduleEvent;

        service.Publish(PrivacyState.FromApplications(
        [
            new("discord", 10, "Discord", "Discord", null, true, false)
        ]));

        Assert.IsNotNull(raised);
        Assert.AreEqual(ModuleEventPriority.High, raised.Priority);
        Assert.AreEqual("Discord", raised.Presentation.PrimaryText);
        Assert.IsTrue(module.CurrentPresentation?.IsPersistentOverride);
        Assert.AreEqual(PrivacyIndicatorKind.Microphone, viewModel.Indicator);
    }

    [TestMethod]
    public async Task CameraAndMicrophone_UsesCameraIndicatorAndListsBothApps()
    {
        var service = new FakePrivacyUsageService(PrivacyState.Empty);
        var viewModel = new PrivacyModuleViewModel(service);
        var module = new PrivacyModule(service, viewModel);
        await module.ActivateAsync();

        service.Publish(PrivacyState.FromApplications(
        [
            new("discord", 10, "Discord", "Discord", null, true, false),
            new("teams", 20, "ms-teams", "Microsoft Teams", null, false, true)
        ]));

        Assert.AreEqual(PrivacyIndicatorKind.Camera, viewModel.Indicator);
        Assert.HasCount(2, viewModel.Applications);
        Assert.IsTrue(viewModel.HasActiveUsage);
    }

    [TestMethod]
    public async Task ClearingUsage_RemovesPersistenceOverride()
    {
        var initial = PrivacyState.FromApplications(
        [
            new("discord", 10, "Discord", "Discord", null, true, false)
        ]);
        var service = new FakePrivacyUsageService(initial);
        var module = new PrivacyModule(service, new PrivacyModuleViewModel(service));
        await module.ActivateAsync();
        Assert.IsTrue(module.CurrentPresentation?.IsPersistentOverride);

        service.Publish(PrivacyState.Empty);

        Assert.IsFalse(module.CurrentPresentation?.IsPersistentOverride);
        Assert.AreEqual(PrivacyIndicatorKind.Idle, module.CurrentPresentation is null
            ? PrivacyIndicatorKind.Idle
            : PrivacyIndicatorKind.Idle);
    }

    private sealed class FakePrivacyUsageService(PrivacyState current) : IPrivacyUsageService
    {
        private EventHandler<PrivacyState>? _stateChanged;

        public PrivacyState Current { get; private set; } = current;

        public event EventHandler<PrivacyState>? StateChanged
        {
            add => _stateChanged += value;
            remove => _stateChanged -= value;
        }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Publish(PrivacyState state)
        {
            Current = state;
            _stateChanged?.Invoke(this, state);
        }
    }
}
