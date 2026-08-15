using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using MiaDock.Modules.DeviceStatus.Settings;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.Services;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class DeviceHubServiceTests
{
    [TestMethod]
    public async Task InitialSnapshot_ListsDevicesWithoutConnectedEvents()
    {
        var bluetooth = new FakeBluetooth(new BluetoothStatusSnapshot(
            DeviceServiceState.Ready, true, [new BluetoothDeviceState("headset", "Headset", true, true, EndpointId: "aep-headset")], BluetoothRadioState.On));
        await using var hub = CreateHub(bluetooth, new FakeAudioCatalog(), new FakeStorage());
        var events = new List<DeviceHubChange>();
        hub.DeviceChanged += (_, change) => events.Add(change);

        await hub.StartAsync();

        Assert.HasCount(1, hub.Current.BluetoothDevices);
        Assert.IsEmpty(events);
        Assert.IsTrue(hub.Current.BluetoothDevices[0].CanDisconnect);
        Assert.IsFalse(hub.Current.BluetoothDevices[0].CanConnect);
        Assert.AreEqual("aep-headset", hub.Current.BluetoothDevices[0].NativeDeviceId);
    }

    [TestMethod]
    public async Task DisconnectedBluetooth_ExposesConnectWhenEndpointExists()
    {
        var bluetooth = new FakeBluetooth(new BluetoothStatusSnapshot(
            DeviceServiceState.Ready,
            true,
            [new BluetoothDeviceState("buds", "Buds", false, true, EndpointId: "aep-buds")],
            BluetoothRadioState.On));
        await using var hub = CreateHub(bluetooth, new FakeAudioCatalog(), new FakeStorage());

        await hub.StartAsync();

        Assert.IsTrue(hub.Current.BluetoothDevices[0].CanConnect);
        Assert.IsFalse(hub.Current.BluetoothDevices[0].CanDisconnect);
        Assert.AreEqual("aep-buds", hub.Current.BluetoothDevices[0].NativeDeviceId);
    }

    [TestMethod]
    public async Task RuntimeConnection_IsDeduplicated()
    {
        var bluetooth = new FakeBluetooth(Ready());
        await using var hub = CreateHub(bluetooth, new FakeAudioCatalog(), new FakeStorage());
        var events = new List<DeviceHubChange>();
        hub.DeviceChanged += (_, change) => events.Add(change);
        await hub.StartAsync();

        bluetooth.Publish(Ready(new BluetoothDeviceState("headset", "Headset", true, true)));
        await hub.RefreshAsync();
        await hub.RefreshAsync();

        Assert.HasCount(1, events.Where(change => change.Kind == DeviceHubChangeKind.Connected));
    }

    [TestMethod]
    public async Task DefaultOutputChange_RaisesOneAudioChange()
    {
        var audio = new FakeAudioCatalog
        {
            Outputs = [new AudioDeviceInfo("speaker", "Speakers", true, false, true)]
        };
        await using var hub = CreateHub(new FakeBluetooth(Ready()), audio, new FakeStorage());
        var events = new List<DeviceHubChange>();
        hub.DeviceChanged += (_, change) => events.Add(change);
        await hub.StartAsync();
        audio.Outputs = [new AudioDeviceInfo("headset", "Headset", true, false, true)];

        await hub.RefreshAsync();

        Assert.HasCount(1, events.Where(change => change.Kind == DeviceHubChangeKind.DefaultAudioOutputChanged));
        Assert.AreEqual("headset", hub.Current.AudioOutputDevices.Single(device => device.IsDefault).Id);
    }

    [TestMethod]
    public async Task StorageRemoval_DoesNotLeaveStaleEntries()
    {
        var storage = new FakeStorage
        {
            Devices = [new RemovableStorageInfo("E:", "USB", "E:\\", "FAT32", 100, 50, true)]
        };
        await using var hub = CreateHub(new FakeBluetooth(Ready()), new FakeAudioCatalog(), storage);
        await hub.StartAsync();
        storage.Devices = [];

        await hub.RefreshAsync();

        Assert.IsEmpty(hub.Current.StorageDevices);
    }

    [TestMethod]
    public async Task BatteryWarnings_FireOncePerThresholdAndResetAfterReconnect()
    {
        var bluetooth = new FakeBluetooth(Ready(
            new BluetoothDeviceState("mouse", "Mouse", true, true, 21, DeviceHubDeviceType.Mouse)));
        await using var hub = CreateHub(bluetooth, new FakeAudioCatalog(), new FakeStorage());
        var events = new List<DeviceHubChange>();
        hub.DeviceChanged += (_, change) => events.Add(change);
        await hub.StartAsync();

        bluetooth.Publish(Ready(new BluetoothDeviceState("mouse", "Mouse", true, true, 20, DeviceHubDeviceType.Mouse)));
        await hub.RefreshAsync();
        bluetooth.Publish(Ready(new BluetoothDeviceState("mouse", "Mouse", true, true, 19, DeviceHubDeviceType.Mouse)));
        await hub.RefreshAsync();
        bluetooth.Publish(Ready(new BluetoothDeviceState("mouse", "Mouse", true, true, 10, DeviceHubDeviceType.Mouse)));
        await hub.RefreshAsync();
        bluetooth.Publish(Ready(new BluetoothDeviceState("mouse", "Mouse", true, true, 5, DeviceHubDeviceType.Mouse)));
        await hub.RefreshAsync();

        Assert.AreEqual(3, events.Count(change => change.Kind == DeviceHubChangeKind.BatteryLow));

        bluetooth.Publish(Ready(new BluetoothDeviceState("mouse", "Mouse", false, false, 20, DeviceHubDeviceType.Mouse)));
        await hub.RefreshAsync();
        bluetooth.Publish(Ready(new BluetoothDeviceState("mouse", "Mouse", true, true, 20, DeviceHubDeviceType.Mouse)));
        await hub.RefreshAsync();

        Assert.AreEqual(4, events.Count(change => change.Kind == DeviceHubChangeKind.BatteryLow));
    }

    private static DeviceHubService CreateHub(
        FakeBluetooth bluetooth,
        FakeAudioCatalog audio,
        FakeStorage storage) => new(
            bluetooth, audio, storage, new FakeSystemActivity(), new FakeSettings());

    private static BluetoothStatusSnapshot Ready(params BluetoothDeviceState[] devices) =>
        new(DeviceServiceState.Ready, true, devices, BluetoothRadioState.On);

    private sealed class FakeBluetooth(BluetoothStatusSnapshot current) : IBluetoothStatusService
    {
        public BluetoothStatusSnapshot Current { get; private set; } = current;
        public event EventHandler<BluetoothStatusSnapshot>? SnapshotChanged;
        public ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IAsyncDisposable>(new FakeLease());
        public void Publish(BluetoothStatusSnapshot value) { Current = value; SnapshotChanged?.Invoke(this, value); }
        public void Dispose() { }
    }

    private sealed class FakeLease : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeAudioCatalog : IAudioDeviceCatalog
    {
        public IReadOnlyList<AudioDeviceInfo> Outputs { get; set; } = [];
        public IReadOnlyList<AudioDeviceInfo> Inputs { get; set; } = [];
        public Task<IReadOnlyList<AudioDeviceInfo>> GetOutputDevicesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Outputs);
        public Task<IReadOnlyList<AudioDeviceInfo>> GetInputDevicesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Inputs);
    }

    private sealed class FakeStorage : IRemovableStorageService
    {
        public IReadOnlyList<RemovableStorageInfo> Devices { get; set; } = [];
        public Task<IReadOnlyList<RemovableStorageInfo>> GetDevicesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Devices);
        public Task<bool> OpenAsync(RemovableStorageInfo storage, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<RemovableStorageEjectResult> EjectAsync(RemovableStorageInfo storage, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RemovableStorageEjectResult(RemovableStorageEjectStatus.Succeeded));
        public Task<bool> OpenSafelyRemoveHardwareAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeSettings : IDeviceHubSettings
    {
        public DeviceHubOptions Current { get; } = DeviceHubOptions.Default;
        public event EventHandler<DeviceHubOptions>? Changed { add { } remove { } }
    }

    private sealed class FakeSystemActivity : ISystemActivityService
    {
        public SystemActivitySnapshot Current => SystemActivitySnapshot.Default;
        public event EventHandler<SystemActivitySnapshot>? SnapshotChanged { add { } remove { } }
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> SetMasterVolumeAsync(double volume, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ToggleMasterMuteAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> SetApplicationVolumeAsync(double volume, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> ToggleApplicationMuteAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
