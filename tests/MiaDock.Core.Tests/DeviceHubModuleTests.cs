using MiaDock.Modules.DeviceStatus;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using MiaDock.Modules.DeviceStatus.Settings;
using MiaDock.Modules.DeviceStatus.ViewModels;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class DeviceHubModuleTests
{
    [TestMethod]
    public async Task StorageConnection_IsSensitiveAndUsesOpaqueActionCommand()
    {
        var service = new FakeDeviceHubService();
        var storage = new FakeStorage();
        var viewModel = new DeviceHubViewModel(service, storage, new FakeSettingsLauncher());
        var module = new DeviceHubModule(service, viewModel, new FakeSettings());
        MiaDock.Core.Modules.ModuleEvent? raised = null;
        module.EventOccurred += (_, value) => raised = value;
        await module.ActivateAsync();
        var device = new DeviceHubDevice(
            "USB\\VID_SECRET",
            "Archive",
            DeviceHubDeviceCategory.RemovableStorage,
            DeviceHubConnectionState.Connected,
            false,
            null,
            DeviceHubDeviceCapabilities.Open,
            NativeDeviceId: "E:\\");

        service.Publish(new DeviceHubChange(DeviceHubChangeKind.Connected, device));

        Assert.IsNotNull(raised);
        Assert.IsTrue(raised.Presentation.IsSensitive);
        Assert.AreEqual(MiaDock.Core.Modules.AudibleNotificationCue.DeviceConnected, raised.AudibleCue);
        var command = raised.Presentation.Commands.Single();
        Assert.DoesNotContain("VID_SECRET", command.Id);
        Assert.IsTrue(module.CanExecuteCommand(command.Id));
        Assert.IsTrue(await module.ExecuteCommandAsync(command.Id));
        Assert.IsTrue(storage.Opened);

        await module.DisposeAsync();
        viewModel.Dispose();
    }

    [TestMethod]
    public async Task BatteryAndDisconnectChanges_UseExpectedAudibleCues()
    {
        var service = new FakeDeviceHubService();
        var viewModel = new DeviceHubViewModel(service, new FakeStorage(), new FakeSettingsLauncher());
        var module = new DeviceHubModule(service, viewModel, new FakeSettings());
        var events = new List<MiaDock.Core.Modules.ModuleEvent>();
        module.EventOccurred += (_, value) => events.Add(value);
        await module.ActivateAsync();
        var device = new DeviceHubDevice(
            "bluetooth-device",
            "Headset",
            DeviceHubDeviceCategory.Bluetooth,
            DeviceHubConnectionState.Connected,
            false,
            10,
            DeviceHubDeviceCapabilities.HasBattery);

        service.Publish(new DeviceHubChange(DeviceHubChangeKind.BatteryLow, device));
        service.Publish(new DeviceHubChange(DeviceHubChangeKind.Disconnected,
            device with { ConnectionState = DeviceHubConnectionState.Disconnected }, device));

        Assert.AreEqual(MiaDock.Core.Modules.AudibleNotificationCue.LowBattery, events[0].AudibleCue);
        Assert.AreEqual(MiaDock.Core.Modules.AudibleNotificationCue.DeviceDisconnected, events[1].AudibleCue);

        await module.DisposeAsync();
        viewModel.Dispose();
    }

    private sealed class FakeDeviceHubService : IDeviceHubService
    {
        public DeviceHubState Current { get; private set; } = DeviceHubState.Default;
        public event EventHandler<DeviceHubState>? StateChanged { add { } remove { } }
        public event EventHandler<DeviceHubChange>? DeviceChanged;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void NotifySafeToRemove(DeviceHubDevice device) { }
        public void Publish(DeviceHubChange change) => DeviceChanged?.Invoke(this, change);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeStorage : IRemovableStorageService
    {
        public bool Opened { get; private set; }
        public Task<IReadOnlyList<RemovableStorageInfo>> GetDevicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RemovableStorageInfo>>([]);
        public Task<bool> OpenAsync(RemovableStorageInfo storage, CancellationToken cancellationToken = default)
        {
            Opened = true;
            return Task.FromResult(true);
        }
        public Task<RemovableStorageEjectResult> EjectAsync(RemovableStorageInfo storage, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RemovableStorageEjectResult(RemovableStorageEjectStatus.Succeeded));
        public Task<bool> OpenSafelyRemoveHardwareAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeSettingsLauncher : IDeviceHubSettingsLauncher
    {
        public Task<bool> OpenBluetoothSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> OpenSoundSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeSettings : IDeviceHubSettings
    {
        public DeviceHubOptions Current { get; } = DeviceHubOptions.Default;
        public event EventHandler<DeviceHubOptions>? Changed { add { } remove { } }
    }
}
