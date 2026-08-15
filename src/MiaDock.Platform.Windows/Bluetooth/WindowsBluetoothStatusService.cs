using MiaDock.Core.Logging;
using MiaDock.Core.Threading;
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
    private const string BatteryLifeProperty = "System.Devices.BatteryLife";
    private const string CategoryIdsProperty = "System.Devices.CategoryIds";
    private const string AepCategoryProperty = "System.Devices.Aep.Category";
    private readonly IUiDispatcher _dispatcher;
    private readonly IBluetoothRadioStateProvider _radioProvider;
    private readonly ILogService? _log;
    private readonly object _gate = new();
    private readonly Dictionary<string, DeviceInformation> _devices = new(StringComparer.Ordinal);
    private DeviceWatcher? _watcher;
    private BluetoothRadioState _radioState = BluetoothRadioState.Unknown;
    private long _watcherGeneration;
    private long _publishRevision;
    private bool _enumerationComplete;
    private bool _started;
    private int _leaseCount;
    private bool _disposed;

    public WindowsBluetoothStatusService(
        IUiDispatcher dispatcher,
        IBluetoothRadioStateProvider radioProvider,
        ILogService? log = null)
    {
        _dispatcher = dispatcher;
        _radioProvider = radioProvider;
        _log = log;
    }

    public BluetoothStatusSnapshot Current { get; private set; } = BluetoothStatusSnapshot.Default;
    public event EventHandler<BluetoothStatusSnapshot>? SnapshotChanged;

    public async ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        var shouldStart = false;
        lock (_gate)
        {
            _leaseCount++;
            shouldStart = _leaseCount == 1;
        }

        if (!shouldStart)
        {
            return new Lease(this);
        }

        try
        {
            await StartCoreAsync(cancellationToken).ConfigureAwait(false);
            return new Lease(this);
        }
        catch
        {
            await ReleaseLeaseAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async ValueTask StartCoreAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _started = true;
            _radioProvider.StateChanged += OnRadioStateChanged;
        }

        Publish(BluetoothRadioStatePolicy.CreateNonDiscoveringSnapshot(BluetoothRadioState.Unknown));
        try
        {
            await _radioProvider.StartAsync(cancellationToken);
            ApplyRadioState(_radioProvider.Current);
        }
        catch (OperationCanceledException)
        {
            await StopCoreAsync().ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            ApplyRadioState(BluetoothRadioState.Unavailable);
            _log?.Write(
                TechnicalLogLevel.Warning,
                TechnicalEventIds.DeviceStatusUnavailable,
                "DeviceStatus",
                "Bluetooth radio state is unavailable.",
                exception,
                new Dictionary<string, object?> { ["service"] = "bluetooth-radio" });
        }
    }

    private ValueTask StopCoreAsync()
    {
        lock (_gate)
        {
            if (!_started)
            {
                return ValueTask.CompletedTask;
            }
            _started = false;
            _radioProvider.StateChanged -= OnRadioStateChanged;
            _radioProvider.Stop();
            StopWatcherLocked();
            _radioState = BluetoothRadioState.Unknown;
            _devices.Clear();
            _enumerationComplete = false;
        }
        Publish(BluetoothStatusSnapshot.Default, allowStopped: true);
        return ValueTask.CompletedTask;
    }

    private async ValueTask ReleaseLeaseAsync()
    {
        var shouldStop = false;
        lock (_gate)
        {
            if (_leaseCount == 0)
            {
                return;
            }

            _leaseCount--;
            shouldStop = _leaseCount == 0;
        }

        if (shouldStop)
        {
            await StopCoreAsync().ConfigureAwait(false);
        }
    }

    private void OnRadioStateChanged(object? sender, BluetoothRadioState state) => ApplyRadioState(state);

    private void ApplyRadioState(BluetoothRadioState state)
    {
        BluetoothStatusSnapshot snapshot;
        lock (_gate)
        {
            if (!_started || _disposed || state == _radioState && state == BluetoothRadioState.On && _watcher is not null)
            {
                return;
            }

            _radioState = state;
            if (state == BluetoothRadioState.On)
            {
                snapshot = StartWatcherLocked();
            }
            else
            {
                StopWatcherLocked();
                _devices.Clear();
                _enumerationComplete = false;
                snapshot = BluetoothRadioStatePolicy.CreateNonDiscoveringSnapshot(state);
            }
        }
        Publish(snapshot);
    }

    private BluetoothStatusSnapshot StartWatcherLocked()
    {
        if (_watcher is not null)
        {
            return BuildSnapshotLocked(DeviceServiceState.Starting);
        }

        _devices.Clear();
        _enumerationComplete = false;
        var selector = $"(System.Devices.Aep.ProtocolId:=\"{ClassicProtocol}\" OR System.Devices.Aep.ProtocolId:=\"{LowEnergyProtocol}\") AND System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True";
        try
        {
            var watcher = DeviceInformation.CreateWatcher(
                selector,
                new[]
                {
                    IsConnectedProperty,
                    IsPresentProperty,
                    ContainerIdProperty,
                    BatteryLifeProperty,
                    CategoryIdsProperty,
                    AepCategoryProperty
                },
                DeviceInformationKind.AssociationEndpoint);
            _watcherGeneration++;
            _watcher = watcher;
            watcher.Added += OnAdded;
            watcher.Updated += OnUpdated;
            watcher.Removed += OnRemoved;
            watcher.EnumerationCompleted += OnEnumerationCompleted;
            watcher.Stopped += OnStopped;
            watcher.Start();
            _log?.Write(
                TechnicalLogLevel.Information,
                TechnicalEventIds.BluetoothWatcherReady,
                "DeviceStatus",
                "Bluetooth device watcher started.",
                properties: new Dictionary<string, object?> { ["generation"] = _watcherGeneration });
            return BuildSnapshotLocked(DeviceServiceState.Starting);
        }
        catch (Exception exception)
        {
            StopWatcherLocked();
            _log?.Write(
                TechnicalLogLevel.Warning,
                TechnicalEventIds.DeviceStatusUnavailable,
                "DeviceStatus",
                "Bluetooth device watcher is unavailable.",
                exception,
                new Dictionary<string, object?> { ["service"] = "bluetooth-watcher" });
            return new BluetoothStatusSnapshot(
                DeviceServiceState.Faulted,
                false,
                Array.Empty<BluetoothDeviceState>(),
                BluetoothRadioState.On);
        }
    }

    private void OnAdded(DeviceWatcher sender, DeviceInformation device)
    {
        long generation;
        lock (_gate)
        {
            if (!TryCaptureGeneration(sender, out generation)) return;
            _devices[device.Id] = device;
        }
        PublishCurrent(generation);
    }

    private void OnUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        long generation;
        lock (_gate)
        {
            if (!TryCaptureGeneration(sender, out generation)) return;
            if (_devices.TryGetValue(update.Id, out var device)) device.Update(update);
        }
        PublishCurrent(generation);
    }

    private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        long generation;
        lock (_gate)
        {
            if (!TryCaptureGeneration(sender, out generation)) return;
            _devices.Remove(update.Id);
        }
        PublishCurrent(generation);
    }

    private void OnEnumerationCompleted(DeviceWatcher sender, object args)
    {
        long generation;
        lock (_gate)
        {
            if (!TryCaptureGeneration(sender, out generation)) return;
            _enumerationComplete = true;
        }
        PublishCurrent(generation);
    }

    private void OnStopped(DeviceWatcher sender, object args)
    {
        long generation;
        lock (_gate)
        {
            if (!TryCaptureGeneration(sender, out generation)) return;
        }
        Publish(new BluetoothStatusSnapshot(
            DeviceServiceState.Faulted,
            false,
            Array.Empty<BluetoothDeviceState>(),
            _radioState));
    }

    private void PublishCurrent(long generation)
    {
        BluetoothStatusSnapshot snapshot;
        lock (_gate)
        {
            if (!_started || _disposed || generation != _watcherGeneration || _radioState != BluetoothRadioState.On)
            {
                return;
            }
            snapshot = BuildSnapshotLocked(DeviceServiceState.Ready);
        }
        Publish(snapshot);
    }

    private BluetoothStatusSnapshot BuildSnapshotLocked(DeviceServiceState state)
    {
        var devices = BluetoothDeviceStateReducer.Merge(_devices.Values.Select(ToState));
        return new BluetoothStatusSnapshot(state, _enumerationComplete, devices, _radioState);
    }

    private bool TryCaptureGeneration(DeviceWatcher sender, out long generation)
    {
        generation = _watcherGeneration;
        return _started && !_disposed && _radioState == BluetoothRadioState.On && ReferenceEquals(sender, _watcher);
    }

    private static BluetoothDeviceState ToState(DeviceInformation device)
    {
        var container = GetGuid(device, ContainerIdProperty)?.ToString("N") ?? device.Id;
        return new BluetoothDeviceState(
            container,
            string.IsNullOrWhiteSpace(device.Name) ? "Bluetooth cihazı" : device.Name,
            GetBool(device, IsConnectedProperty),
            GetBool(device, IsPresentProperty),
            GetBatteryPercentage(device),
            ClassifyDevice(device));
    }

    private static bool GetBool(DeviceInformation device, string key) =>
        device.Properties.TryGetValue(key, out var value) && value is bool result && result;

    private static Guid? GetGuid(DeviceInformation device, string key) =>
        device.Properties.TryGetValue(key, out var value) && value is Guid result ? result : null;

    private static int? GetBatteryPercentage(DeviceInformation device, string key = BatteryLifeProperty)
    {
        if (!device.Properties.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        var percentage = value switch
        {
            byte byteValue => byteValue,
            short shortValue => shortValue,
            int intValue => intValue,
            uint uintValue when uintValue <= int.MaxValue => (int)uintValue,
            _ => -1
        };
        return percentage is >= 0 and <= 100 ? percentage : null;
    }

    private static DeviceHubDeviceType ClassifyDevice(DeviceInformation device)
    {
        var categories = ReadStrings(device, CategoryIdsProperty)
            .Concat(ReadStrings(device, AepCategoryProperty))
            .ToArray();
        if (categories.Any(value => Contains(value, "Audio.Headphone"))) return DeviceHubDeviceType.Headphones;
        if (categories.Any(value => Contains(value, "Audio.Headset"))) return DeviceHubDeviceType.Headset;
        if (categories.Any(value => Contains(value, "Audio.Speaker"))) return DeviceHubDeviceType.Speaker;
        if (categories.Any(value => Contains(value, "Input.Mouse"))) return DeviceHubDeviceType.Mouse;
        if (categories.Any(value => Contains(value, "Input.Keyboard"))) return DeviceHubDeviceType.Keyboard;
        if (categories.Any(value => Contains(value, "Input.Gaming") || Contains(value, "Game"))) return DeviceHubDeviceType.Gamepad;
        return DeviceHubDeviceType.GenericBluetoothDevice;
    }

    private static IEnumerable<string> ReadStrings(DeviceInformation device, string key)
    {
        if (!device.Properties.TryGetValue(key, out var value) || value is null)
        {
            return Array.Empty<string>();
        }

        return value switch
        {
            string text => [text],
            string[] values => values,
            IEnumerable<string> values => values,
            _ => Array.Empty<string>()
        };
    }

    private static bool Contains(string value, string expected) =>
        value.Contains(expected, StringComparison.OrdinalIgnoreCase);

    private void Publish(BluetoothStatusSnapshot snapshot, bool allowStopped = false)
    {
        long revision;
        lock (_gate)
        {
            revision = ++_publishRevision;
        }

        void Apply()
        {
            lock (_gate)
            {
                if (_disposed || revision != _publishRevision || (!allowStopped && !_started))
                {
                    return;
                }
                Current = snapshot;
            }
            SnapshotChanged?.Invoke(this, snapshot);
        }

        if (_dispatcher.HasThreadAccess) Apply(); else _dispatcher.TryEnqueue(Apply);
    }

    private void StopWatcherLocked()
    {
        var watcher = _watcher;
        _watcher = null;
        _watcherGeneration++;
        if (watcher is null) return;
        watcher.Added -= OnAdded;
        watcher.Updated -= OnUpdated;
        watcher.Removed -= OnRemoved;
        watcher.EnumerationCompleted -= OnEnumerationCompleted;
        watcher.Stopped -= OnStopped;
        if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
        {
            watcher.Stop();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _leaseCount = 0;
            _started = false;
            _publishRevision++;
            _radioProvider.StateChanged -= OnRadioStateChanged;
            _radioProvider.Stop();
            StopWatcherLocked();
            _devices.Clear();
        }
    }

    private sealed class Lease(WindowsBluetoothStatusService owner) : IAsyncDisposable
    {
        private WindowsBluetoothStatusService? _owner = owner;

        public ValueTask DisposeAsync()
        {
            var current = Interlocked.Exchange(ref _owner, null);
            return current is null ? ValueTask.CompletedTask : current.ReleaseLeaseAsync();
        }
    }
}
