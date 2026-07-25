using MiaDock.Core.Threading;
using MiaDock.Core.Logging;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using Windows.Devices.Enumeration;

namespace MiaDock.Platform.Windows.Bluetooth;

public sealed class WindowsBluetoothStatusService : IBluetoothStatusService
{
    private const string ClassicProtocol = "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}";
    private const string LowEnergyProtocol = "{bb7bb05e-5972-42b5-94fc-76eaa7084d49}";
    private const string IsConnectedProperty = "System.Devices.Aep.IsConnected";
    private const string IsPresentProperty = "System.Devices.Aep.IsPresent";
    private const string ContainerIdProperty = "System.Devices.Aep.ContainerId";
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogService? _log;
    private readonly object _gate = new();
    private readonly Dictionary<string, DeviceInformation> _devices = new(StringComparer.Ordinal);
    private DeviceWatcher? _watcher;
    private bool _enumerationComplete;
    private bool _disposed;

    public WindowsBluetoothStatusService(IUiDispatcher dispatcher, ILogService? log = null)
    {
        _dispatcher = dispatcher;
        _log = log;
    }

    public BluetoothStatusSnapshot Current { get; private set; } = BluetoothStatusSnapshot.Default;
    public event EventHandler<BluetoothStatusSnapshot>? SnapshotChanged;

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            if (_watcher is not null) return ValueTask.CompletedTask;
            _devices.Clear();
            _enumerationComplete = false;
            try
            {
                var selector = $"(System.Devices.Aep.ProtocolId:=\"{ClassicProtocol}\" OR System.Devices.Aep.ProtocolId:=\"{LowEnergyProtocol}\") AND System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True";
                _watcher = DeviceInformation.CreateWatcher(
                    selector,
                    new[] { IsConnectedProperty, IsPresentProperty, ContainerIdProperty },
                    DeviceInformationKind.AssociationEndpoint);
                _watcher.Added += OnAdded;
                _watcher.Updated += OnUpdated;
                _watcher.Removed += OnRemoved;
                _watcher.EnumerationCompleted += OnEnumerationCompleted;
                _watcher.Stopped += OnStopped;
                _watcher.Start();
                Publish(new BluetoothStatusSnapshot(DeviceServiceState.Starting, false, Array.Empty<BluetoothDeviceState>()));
                _log?.Write(TechnicalLogLevel.Information, TechnicalEventIds.BluetoothWatcherReady,
                    "DeviceStatus", "Bluetooth device watcher started.");
            }
            catch (Exception)
            {
                DetachWatcher();
                Publish(BluetoothStatusSnapshot.Default with { State = DeviceServiceState.Unavailable });
                _log?.Write(TechnicalLogLevel.Warning, TechnicalEventIds.DeviceStatusUnavailable,
                    "DeviceStatus", "Bluetooth device watcher is unavailable.", properties: new Dictionary<string, object?> { ["service"] = "bluetooth" });
            }
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_watcher is { Status: DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted })
            {
                _watcher.Stop();
            }
            DetachWatcher();
            _devices.Clear();
            _enumerationComplete = false;
        }
        Publish(BluetoothStatusSnapshot.Default);
        return ValueTask.CompletedTask;
    }

    private void OnAdded(DeviceWatcher sender, DeviceInformation device)
    {
        lock (_gate) _devices[device.Id] = device;
        PublishCurrent();
    }

    private void OnUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        lock (_gate)
        {
            if (_devices.TryGetValue(update.Id, out var device)) device.Update(update);
        }
        PublishCurrent();
    }

    private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        lock (_gate) _devices.Remove(update.Id);
        PublishCurrent();
    }

    private void OnEnumerationCompleted(DeviceWatcher sender, object args)
    {
        lock (_gate) _enumerationComplete = true;
        PublishCurrent();
    }

    private void OnStopped(DeviceWatcher sender, object args)
    {
        if (!_disposed) Publish(Current with { State = DeviceServiceState.Unavailable });
    }

    private void PublishCurrent()
    {
        BluetoothDeviceState[] devices;
        bool completed;
        lock (_gate)
        {
            completed = _enumerationComplete;
            devices = _devices.Values.Select(ToState).ToArray();
        }
        Publish(new BluetoothStatusSnapshot(DeviceServiceState.Ready, completed, BluetoothDeviceStateReducer.Merge(devices)));
    }

    private static BluetoothDeviceState ToState(DeviceInformation device)
    {
        var container = GetGuid(device, ContainerIdProperty)?.ToString("N") ?? device.Id;
        return new BluetoothDeviceState(
            container,
            string.IsNullOrWhiteSpace(device.Name) ? "Bluetooth cihazı" : device.Name,
            GetBool(device, IsConnectedProperty),
            GetBool(device, IsPresentProperty));
    }

    private static bool GetBool(DeviceInformation device, string key) =>
        device.Properties.TryGetValue(key, out var value) && value is bool result && result;

    private static Guid? GetGuid(DeviceInformation device, string key) =>
        device.Properties.TryGetValue(key, out var value) && value is Guid result ? result : null;

    private void Publish(BluetoothStatusSnapshot snapshot)
    {
        void Apply() { Current = snapshot; SnapshotChanged?.Invoke(this, snapshot); }
        if (_dispatcher.HasThreadAccess) Apply(); else _dispatcher.TryEnqueue(Apply);
    }

    private void DetachWatcher()
    {
        if (_watcher is null) return;
        _watcher.Added -= OnAdded;
        _watcher.Updated -= OnUpdated;
        _watcher.Removed -= OnRemoved;
        _watcher.EnumerationCompleted -= OnEnumerationCompleted;
        _watcher.Stopped -= OnStopped;
        _watcher = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_watcher is { Status: DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted }) _watcher.Stop();
        DetachWatcher();
    }
}
