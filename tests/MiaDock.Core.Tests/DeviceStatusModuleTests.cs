using MiaDock.Core.Modules;
using MiaDock.Core.Settings;
using MiaDock.Modules.DeviceStatus;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using MiaDock.Modules.DeviceStatus.Settings;
using MiaDock.Modules.DeviceStatus.ViewModels;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class DeviceStatusModuleTests
{
    [TestMethod]
    public void BatteryOptions_NormalizeInvalidOrderingWithoutAffectingEnvelope()
    {
        var envelope = BatteryModuleOptions.ApplyThresholds(ModuleSettingsEnvelope.BatteryDefault, 4, 40, 18);
        var options = BatteryModuleOptions.FromEnvelope(envelope);

        Assert.IsGreaterThan(options.EmergencyThresholdPercent, options.CriticalThresholdPercent);
        Assert.IsGreaterThan(options.CriticalThresholdPercent, options.LowThresholdPercent);
        Assert.IsLessThanOrEqualTo(50, options.LowThresholdPercent);
    }

    [TestMethod]
    public async Task BatteryModule_CrossingThresholdRaisesOneCoalescedEventAndChargingRearms()
    {
        var service = new FakePowerService(Battery(25));
        var settings = new FakeBatterySettings();
        var module = new BatteryModule(service, new BatteryModuleViewModel(service), settings);
        await module.ActivateAsync();
        var events = new List<ModuleEvent>();
        module.EventOccurred += (_, value) => events.Add(value);

        service.Publish(Battery(20));
        service.Publish(Battery(19));
        service.Publish(Battery(25) with { IsCharging = true });
        service.Publish(Battery(20));

        Assert.HasCount(2, events);
        Assert.IsTrue(events.All(value => value.CoalescingKey == "battery:low"));
        Assert.IsTrue(events.All(value => value.Priority == ModuleEventPriority.Normal));
        Assert.IsTrue(events.All(value => value.AudibleCue == AudibleNotificationCue.LowBattery));
    }

    [TestMethod]
    public async Task BatteryModule_DesktopWithoutBatteryDoesNotOccupyModuleStrip()
    {
        var service = new FakePowerService(Battery(0) with { IsBatteryPresent = false });
        var module = new BatteryModule(service, new BatteryModuleViewModel(service), new FakeBatterySettings());

        await module.ActivateAsync();

        Assert.IsNull(module.CurrentPresentation);
        Assert.IsFalse(module.Descriptor.IsPersistent);
    }

    [TestMethod]
    public async Task NetworkModule_ThroughputChangeDoesNotCreateNotification()
    {
        var service = new FakeNetworkService(Network(NetworkConnectivityKind.Internet));
        var module = new NetworkModule(service, new NetworkModuleViewModel(service));
        await module.ActivateAsync();
        ModuleEvent? raised = null;
        module.EventOccurred += (_, value) => raised = value;

        service.Publish(service.Current with { DownloadBytesPerSecond = 42_000, UploadBytesPerSecond = 2_000 });

        Assert.IsNull(raised);
    }

    [TestMethod]
    public async Task NetworkModule_MapsConnectivityLossToTypedAudibleCuesAndRecoveryToNone()
    {
        var service = new FakeNetworkService(Network(NetworkConnectivityKind.Internet));
        var module = new NetworkModule(service, new NetworkModuleViewModel(service));
        await module.ActivateAsync();
        var events = new List<ModuleEvent>();
        module.EventOccurred += (_, value) => events.Add(value);

        service.Publish(Network(NetworkConnectivityKind.Offline) with { ConnectionKind = NetworkConnectionKind.None });
        service.Publish(Network(NetworkConnectivityKind.LocalAccess) with { ConnectionKind = NetworkConnectionKind.WiFi });
        service.Publish(Network(NetworkConnectivityKind.Internet) with { ConnectionKind = NetworkConnectionKind.WiFi });

        Assert.HasCount(3, events);
        Assert.AreEqual(AudibleNotificationCue.NetworkOffline, events[0].AudibleCue);
        Assert.AreEqual(AudibleNotificationCue.ConnectedWithoutInternet, events[1].AudibleCue);
        Assert.AreEqual(AudibleNotificationCue.None, events[2].AudibleCue);
    }

    [TestMethod]
    public void NetworkThroughputSampling_RunsOnlyWhileExpandedAndStopsOnDispose()
    {
        var service = new FakeNetworkService(Network(NetworkConnectivityKind.Internet));
        var viewModel = new NetworkModuleViewModel(service);

        viewModel.SetExpandedActive(true);
        viewModel.SetExpandedActive(false);
        viewModel.SetExpandedActive(true);
        viewModel.Dispose();

        CollectionAssert.AreEqual(new[] { true, false, true, false }, service.SamplingStates);
    }

    [TestMethod]
    public async Task BluetoothModule_SuppressesInitialEnumerationAndMarksDeviceNameSensitive()
    {
        var initial = new BluetoothStatusSnapshot(DeviceServiceState.Starting, false, Array.Empty<BluetoothDeviceState>(), BluetoothRadioState.On);
        var service = new FakeBluetoothService(initial);
        var module = new BluetoothModule(service, new BluetoothModuleViewModel(service));
        await module.ActivateAsync();
        var events = new List<ModuleEvent>();
        module.EventOccurred += (_, value) => events.Add(value);
        var device = new BluetoothDeviceState("one", "Kulaklık", false, true);

        service.Publish(new BluetoothStatusSnapshot(DeviceServiceState.Ready, true, new[] { device }, BluetoothRadioState.On));
        service.Publish(new BluetoothStatusSnapshot(DeviceServiceState.Ready, true, new[] { device with { IsConnected = true } }, BluetoothRadioState.On));

        Assert.HasCount(1, events);
        Assert.IsTrue(events[0].Presentation.IsSensitive);
        Assert.IsFalse(events[0].IsFullscreenEligible);
        Assert.AreEqual(AudibleNotificationCue.DeviceConnected, events[0].AudibleCue);
    }

    [TestMethod]
    public async Task BluetoothModule_RadioOffInvalidationDoesNotCreateFakeDisconnectEvent()
    {
        var connected = new BluetoothDeviceState("one", "Kulaklık", true, true);
        var service = new FakeBluetoothService(new BluetoothStatusSnapshot(
            DeviceServiceState.Ready,
            true,
            new[] { connected },
            BluetoothRadioState.On));
        var module = new BluetoothModule(service, new BluetoothModuleViewModel(service));
        await module.ActivateAsync();
        var events = new List<ModuleEvent>();
        module.EventOccurred += (_, value) => events.Add(value);

        service.Publish(new BluetoothStatusSnapshot(
            DeviceServiceState.Ready,
            false,
            Array.Empty<BluetoothDeviceState>(),
            BluetoothRadioState.Off));

        Assert.IsEmpty(events);
    }

    [TestMethod]
    public void NetworkViewModel_LanguageChangeUpdatesVisibleStatusWithoutNewSnapshot()
    {
        var localization = new TestLocalizationService(
            new Dictionary<string, (string Turkish, string English)>
            {
                ["Network.Internet"] = ("İnternete bağlı", "Connected to the internet"),
                ["Network.Unmetered"] = ("Tarifesiz bağlantı", "Unmetered connection")
            });
        var service = new FakeNetworkService(Network(NetworkConnectivityKind.Internet));
        using var viewModel = new NetworkModuleViewModel(service, localization);
        var changed = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        localization.SetLanguage(AppLanguage.English);

        Assert.AreEqual("Connected to the internet", viewModel.ConnectivityText);
        Assert.AreEqual("Unmetered connection", viewModel.CostText);
        CollectionAssert.Contains(changed, nameof(NetworkModuleViewModel.ConnectivityText));
        CollectionAssert.Contains(changed, nameof(NetworkModuleViewModel.CostText));
    }

    private static BatteryStatusSnapshot Battery(int percent) => new(
        DeviceServiceState.Ready, true, percent, false, false, "Pil gücü");

    private static NetworkStatusSnapshot Network(NetworkConnectivityKind connectivity) => new(
        DeviceServiceState.Ready, connectivity, NetworkConnectionKind.WiFi, false, Guid.NewGuid(), null, null);

    private sealed class FakeBatterySettings : IBatteryModuleSettings
    {
        public BatteryModuleOptions Current { get; } = BatteryModuleOptions.Default;
        public event EventHandler<BatteryModuleOptions>? Changed { add { } remove { } }
    }

    private sealed class FakePowerService(BatteryStatusSnapshot current) : IPowerStatusService
    {
        public BatteryStatusSnapshot Current { get; private set; } = current;
        public event EventHandler<BatteryStatusSnapshot>? SnapshotChanged;
        public ValueTask StartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public void Publish(BatteryStatusSnapshot value) { Current = value; SnapshotChanged?.Invoke(this, value); }
        public void Dispose() { }
    }

    private sealed class FakeNetworkService(NetworkStatusSnapshot current) : INetworkStatusService
    {
        public NetworkStatusSnapshot Current { get; private set; } = current;
        public List<bool> SamplingStates { get; } = [];
        public event EventHandler<NetworkStatusSnapshot>? SnapshotChanged;
        public ValueTask StartAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public void SetThroughputSamplingEnabled(bool enabled) => SamplingStates.Add(enabled);
        public void Publish(NetworkStatusSnapshot value) { Current = value; SnapshotChanged?.Invoke(this, value); }
        public void Dispose() { }
    }

    private sealed class FakeBluetoothService(BluetoothStatusSnapshot current) : IBluetoothStatusService
    {
        public BluetoothStatusSnapshot Current { get; private set; } = current;
        public event EventHandler<BluetoothStatusSnapshot>? SnapshotChanged;
        public ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IAsyncDisposable>(new FakeBluetoothLease());
        public void Publish(BluetoothStatusSnapshot value) { Current = value; SnapshotChanged?.Invoke(this, value); }
        public void Dispose() { }
    }

    private sealed class FakeBluetoothLease : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
