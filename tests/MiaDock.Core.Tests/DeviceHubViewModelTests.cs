using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using MiaDock.Modules.DeviceStatus.ViewModels;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class DeviceHubViewModelTests
{
    [TestMethod]
    public async Task ConnectBluetooth_SendsNativeDeviceIdAndShowsFailure()
    {
        var connection = new FakeBluetoothConnection { Result = BluetoothConnectionResult.Failed };
        using var viewModel = new DeviceHubViewModel(
            new FakeHub(), new FakeStorage(), new FakeLauncher(), connection);
        var device = new DeviceHubDevice(
            "container",
            "Kulaklık",
            DeviceHubDeviceCategory.Bluetooth,
            DeviceHubConnectionState.Disconnected,
            false,
            null,
            DeviceHubDeviceCapabilities.Connect,
            NativeDeviceId: "endpoint-id",
            DeviceType: DeviceHubDeviceType.Headphones,
            DeviceAddress: "AA:BB:CC:DD:EE:FF");

        await viewModel.ConnectBluetoothDeviceAsync(device);

        Assert.AreEqual("endpoint-id", connection.LastRequest?.EndpointId);
        Assert.AreEqual("AA:BB:CC:DD:EE:FF", connection.LastRequest?.DeviceAddress);
        Assert.IsTrue(viewModel.BluetoothOperationOpen);
        Assert.IsTrue(viewModel.BluetoothOperationError);
        StringAssert.Contains(viewModel.BluetoothOperationMessage, "Kulaklık");
    }

    [TestMethod]
    public async Task ConnectBluetooth_RadioOff_ShowsRadioMessage()
    {
        var connection = new FakeBluetoothConnection { Result = BluetoothConnectionResult.RadioOff };
        using var viewModel = new DeviceHubViewModel(
            new FakeHub(), new FakeStorage(), new FakeLauncher(), connection);
        var device = new DeviceHubDevice(
            "container",
            "Kulaklık",
            DeviceHubDeviceCategory.Bluetooth,
            DeviceHubConnectionState.Disconnected,
            false,
            null,
            DeviceHubDeviceCapabilities.Connect,
            NativeDeviceId: "endpoint-id");

        await viewModel.ConnectBluetoothDeviceAsync(device);

        Assert.IsTrue(viewModel.BluetoothOperationOpen);
        Assert.IsTrue(viewModel.BluetoothOperationError);
        Assert.AreEqual("Turn Bluetooth on and try again.", viewModel.BluetoothOperationMessage);
    }

    [TestMethod]
    public void StateRefresh_ReusesRowsAndRemovesDisconnectedDeviceWithoutDuplicates()
    {
        var hub = new FakeHub();
        using var viewModel = new DeviceHubViewModel(
            hub, new FakeStorage(), new FakeLauncher(), new FakeBluetoothConnection());
        var headphones = new DeviceHubDevice(
            "headphones", "Headphones", DeviceHubDeviceCategory.Bluetooth,
            DeviceHubConnectionState.Connected, false, 72,
            DeviceHubDeviceCapabilities.Disconnect | DeviceHubDeviceCapabilities.HasBattery,
            DeviceType: DeviceHubDeviceType.Headphones);
        var keyboard = new DeviceHubDevice(
            "keyboard", "Keyboard", DeviceHubDeviceCategory.Bluetooth,
            DeviceHubConnectionState.Connected, false, 88,
            DeviceHubDeviceCapabilities.Disconnect | DeviceHubDeviceCapabilities.HasBattery,
            DeviceType: DeviceHubDeviceType.Keyboard);

        hub.PublishState(DeviceHubState.Default with
        {
            IsInitialSnapshot = false,
            BluetoothDevices = [headphones, keyboard]
        });
        var retainedRow = viewModel.BluetoothRows[0];

        hub.PublishState(DeviceHubState.Default with
        {
            IsInitialSnapshot = false,
            BluetoothDevices = [headphones with { BatteryPercentage = 64 }]
        });

        Assert.HasCount(1, viewModel.BluetoothRows);
        Assert.AreSame(retainedRow, viewModel.BluetoothRows[0]);
        Assert.AreEqual("64%", retainedRow.TrailingText);
        Assert.IsFalse(retainedRow.HasDivider);
        Assert.AreEqual("1 connected", viewModel.ConnectedSummary);
    }

    private sealed class FakeHub : IDeviceHubService
    {
        public DeviceHubState Current { get; private set; } = DeviceHubState.Default;
        public event EventHandler<DeviceHubState>? StateChanged;
        public event EventHandler<DeviceHubChange>? DeviceChanged { add { } remove { } }
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void NotifySafeToRemove(DeviceHubDevice device) { }
        public void PublishState(DeviceHubState state)
        {
            Current = state;
            StateChanged?.Invoke(this, state);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeStorage : IRemovableStorageService
    {
        public Task<IReadOnlyList<RemovableStorageInfo>> GetDevicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RemovableStorageInfo>>([]);
        public Task<bool> OpenAsync(RemovableStorageInfo storage, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
        public Task<RemovableStorageEjectResult> EjectAsync(RemovableStorageInfo storage, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RemovableStorageEjectResult(RemovableStorageEjectStatus.Succeeded));
        public Task<bool> OpenSafelyRemoveHardwareAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeLauncher : IDeviceHubSettingsLauncher
    {
        public Task<bool> OpenBluetoothSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> OpenSoundSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeBluetoothConnection : IBluetoothDeviceConnectionService
    {
        public BluetoothConnectionRequest? LastRequest { get; private set; }
        public BluetoothConnectionResult Result { get; set; } = BluetoothConnectionResult.Succeeded;

        public Task<BluetoothConnectionResult> ConnectAsync(
            BluetoothConnectionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }

        public Task<BluetoothConnectionResult> DisconnectAsync(
            BluetoothConnectionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }
}
