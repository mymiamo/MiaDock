using MiaDock.Core.Logging;
using MiaDock.Core.Input;
using MiaDock.Core.Localization;
using MiaDock.Core.Threading;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Settings;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.Services;

namespace MiaDock.Modules.DeviceStatus.Services;

/// <summary>
/// Combines existing device providers into a single, UI-independent snapshot.
/// It owns no WinRT or COM objects; providers remain independently fault tolerant.
/// </summary>
public sealed class DeviceHubService : IDeviceHubService
{
    private static readonly TimeSpan StorageRefreshInterval = TimeSpan.FromSeconds(12);
    private readonly IBluetoothStatusService _bluetooth;
    private readonly IAudioDeviceCatalog _audioDevices;
    private readonly IRemovableStorageService _storage;
    private readonly ISystemActivityService _systemActivity;
    private readonly IDeviceHubSettings _settings;
    private readonly IUiDispatcher? _dispatcher;
    private readonly ILogService? _log;
    private readonly IUsbDeviceMonitor? _usbMonitor;
    private readonly ILocalizationService? _localization;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly Dictionary<string, int> _batteryWarningLevels = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _deduplication = new(StringComparer.Ordinal);
    private CancellationTokenSource? _lifetime;
    private Task? _storageRefreshLoop;
    private IAsyncDisposable? _bluetoothLease;
    private IAsyncDisposable? _usbLease;
    private bool _started;
    private bool _initialBluetoothEnumerationObserved;
    private bool _disposed;

    public DeviceHubService(
        IBluetoothStatusService bluetooth,
        IAudioDeviceCatalog audioDevices,
        IRemovableStorageService storage,
        ISystemActivityService systemActivity,
        IDeviceHubSettings settings,
        IUiDispatcher? dispatcher = null,
        ILogService? log = null,
        IUsbDeviceMonitor? usbMonitor = null,
        ILocalizationService? localization = null)
    {
        _bluetooth = bluetooth;
        _audioDevices = audioDevices;
        _storage = storage;
        _systemActivity = systemActivity;
        _settings = settings;
        _dispatcher = dispatcher;
        _log = log;
        _usbMonitor = usbMonitor;
        _localization = localization;
    }

    public DeviceHubState Current { get; private set; } = DeviceHubState.Default;

    public event EventHandler<DeviceHubState>? StateChanged;

    public event EventHandler<DeviceHubChange>? DeviceChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return;
        }

        _started = true;
        _initialBluetoothEnumerationObserved = false;
        _bluetooth.SnapshotChanged += OnBluetoothSnapshotChanged;
        _systemActivity.SnapshotChanged += OnSystemActivitySnapshotChanged;
        _settings.Changed += OnSettingsChanged;
        if (_usbMonitor is not null)
        {
            _usbMonitor.DeviceChanged += OnUsbDeviceChanged;
        }
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            _bluetoothLease = await _bluetooth.AcquireAsync(cancellationToken).ConfigureAwait(false);
            if (_usbMonitor is not null)
            {
                _usbLease = await _usbMonitor.AcquireAsync(cancellationToken).ConfigureAwait(false);
            }
            await RefreshAsync(cancellationToken).ConfigureAwait(false);
            _storageRefreshLoop = RefreshStorageLoopAsync(_lifetime.Token);
        }
        catch
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        _bluetooth.SnapshotChanged -= OnBluetoothSnapshotChanged;
        _systemActivity.SnapshotChanged -= OnSystemActivitySnapshotChanged;
        _settings.Changed -= OnSettingsChanged;
        if (_usbMonitor is not null)
        {
            _usbMonitor.DeviceChanged -= OnUsbDeviceChanged;
        }
        var lifetime = Interlocked.Exchange(ref _lifetime, null);
        lifetime?.Cancel();

        try
        {
            if (_storageRefreshLoop is not null)
            {
                await _storageRefreshLoop.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected while a module is deactivating.
        }
        finally
        {
            _storageRefreshLoop = null;
            lifetime?.Dispose();
        }

        if (Interlocked.Exchange(ref _usbLease, null) is { } usbLease)
        {
            await usbLease.DisposeAsync().ConfigureAwait(false);
        }
        if (Interlocked.Exchange(ref _bluetoothLease, null) is { } bluetoothLease)
        {
            await bluetoothLease.DisposeAsync().ConfigureAwait(false);
        }
        PublishState(DeviceHubState.Default);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!_started)
        {
            return;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var audioOutputsTask = GetOutputsSafelyAsync(cancellationToken);
            var audioInputsTask = GetInputsSafelyAsync(cancellationToken);
            var storageTask = GetStorageSafelyAsync(cancellationToken);
            await Task.WhenAll(audioOutputsTask, audioInputsTask, storageTask).ConfigureAwait(false);
            Publish(BuildState(
                _bluetooth.Current,
                audioOutputsTask.Result,
                audioInputsTask.Result,
                storageTask.Result));
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void NotifySafeToRemove(DeviceHubDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        if (_started && device.Category == DeviceHubDeviceCategory.RemovableStorage)
        {
            RaiseDeduplicated(DeviceHubChangeKind.SafeToRemove, device);
        }
    }

    private async Task RefreshStorageLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(StorageRefreshInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await RefreshAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogProviderFailure("storage-refresh", exception);
            }
        }
    }

    private void OnBluetoothSnapshotChanged(object? sender, BluetoothStatusSnapshot snapshot) =>
        _ = RefreshFromCallbackAsync();

    private void OnSystemActivitySnapshotChanged(object? sender, SystemActivitySnapshot snapshot) =>
        _ = RefreshFromCallbackAsync();

    private void OnSettingsChanged(object? sender, DeviceHubOptions options) =>
        _ = RefreshFromCallbackAsync();

    private void OnUsbDeviceChanged(object? sender, UsbDeviceChangedEventArgs args) =>
        _ = RefreshFromCallbackAsync();

    private async Task RefreshFromCallbackAsync()
    {
        try
        {
            await RefreshAsync(_lifetime?.Token ?? CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Module has been disabled.
        }
        catch (Exception exception)
        {
            LogProviderFailure("callback-refresh", exception);
        }
    }

    private DeviceHubState BuildState(
        BluetoothStatusSnapshot bluetooth,
        IReadOnlyList<AudioDeviceInfo> outputs,
        IReadOnlyList<AudioDeviceInfo> inputs,
        IReadOnlyList<RemovableStorageInfo> storage)
    {
        var options = _settings.Current;
        var bluetoothDevices = options.ShowBluetooth ? bluetooth.Devices
            .OrderBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(device => new DeviceHubDevice(
                device.Id,
                device.DisplayName,
                DeviceHubDeviceCategory.Bluetooth,
                device.IsConnected ? DeviceHubConnectionState.Connected : DeviceHubConnectionState.Disconnected,
                false,
                device.BatteryPercentage,
                BluetoothCapabilities(device),
                Detail: Text(
                    "DeviceHub.BluetoothDetailFormat",
                    "{0} · {1}",
                    DeviceTypeText(device.DeviceType),
                    device.IsConnected
                        ? Text("DeviceHub.ConnectedState", "Connected")
                        : Text("DeviceHub.DisconnectedState", "Disconnected")),
                NativeDeviceId: device.EndpointId,
                LastChangedAt: DateTimeOffset.UtcNow,
                DeviceType: device.DeviceType,
                DeviceAddress: device.DeviceAddress))
            .ToArray() : [];
        var outputDevices = options.ShowAudioDevices ? outputs
            .OrderByDescending(device => device.IsDefault)
            .ThenBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(device => new DeviceHubDevice(
                device.Id,
                device.DisplayName,
                DeviceHubDeviceCategory.AudioOutput,
                device.IsActive ? DeviceHubConnectionState.Connected : DeviceHubConnectionState.Disconnected,
                device.IsDefault,
                null,
                DeviceHubDeviceCapabilities.ManageInSettings,
                Detail: device.IsDefault ? Text("DeviceHub.Default", "Default") : null,
                NativeDeviceId: device.Id))
            .ToArray() : [];
        var inputDevices = options.ShowAudioDevices ? inputs
            .OrderByDescending(device => device.IsDefault)
            .ThenBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(device => new DeviceHubDevice(
                device.Id,
                device.DisplayName,
                DeviceHubDeviceCategory.AudioInput,
                device.IsActive ? DeviceHubConnectionState.Connected : DeviceHubConnectionState.Disconnected,
                device.IsDefault,
                null,
                DeviceHubDeviceCapabilities.ManageInSettings,
                Detail: device.IsDefault ? Text("DeviceHub.Default", "Default") : null,
                NativeDeviceId: device.Id))
            .ToArray() : [];
        var storageDevices = options.ShowRemovableStorage ? storage
            .OrderBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(device => new DeviceHubDevice(
                device.Id,
                device.DisplayName,
                DeviceHubDeviceCategory.RemovableStorage,
                device.IsReady ? DeviceHubConnectionState.Connected : DeviceHubConnectionState.Unknown,
                false,
                null,
                device.CanEject
                    ? DeviceHubDeviceCapabilities.Open | DeviceHubDeviceCapabilities.Eject
                    : DeviceHubDeviceCapabilities.Open | DeviceHubDeviceCapabilities.ManageInSettings,
                device.FreeSpace is { } free
                    ? Text("DeviceHub.FreeSpaceFormat", "{0} free", FormatBytes(free))
                    : null,
                device.RootPath,
                FileSystem: device.FileSystem,
                TotalSpace: device.TotalSpace,
                FreeSpace: device.FreeSpace,
                CanEject: device.CanEject,
                DeviceInstanceId: device.DeviceInstanceId))
            .ToArray() : [];

        var state = bluetooth.State == DeviceServiceState.Faulted
            ? DeviceServiceState.Faulted
            : DeviceServiceState.Ready;
        return new DeviceHubState(
            state,
            !_initialBluetoothEnumerationObserved || !bluetooth.IsEnumerationComplete,
            bluetoothDevices,
            outputDevices,
            inputDevices,
            storageDevices);
    }

    private void Publish(DeviceHubState next)
    {
        if (!_started)
        {
            return;
        }

        void PublishCore()
        {
            var previous = Current;
            Current = next;
            if (!_initialBluetoothEnumerationObserved && _bluetooth.Current.IsEnumerationComplete)
            {
                _initialBluetoothEnumerationObserved = true;
                Current = next with { IsInitialSnapshot = false };
                StateChanged?.Invoke(this, Current);
                return;
            }

            StateChanged?.Invoke(this, Current);
            if (previous.IsInitialSnapshot || next.IsInitialSnapshot) return;

            PublishTransitions(previous.BluetoothDevices, next.BluetoothDevices);
            PublishTransitions(previous.StorageDevices, next.StorageDevices);
            PublishDefaultOutputChange(previous.AudioOutputDevices, next.AudioOutputDevices);
            PublishBatteryWarnings(next.BatteryDevices);
        }

        if (_dispatcher is null || _dispatcher.HasThreadAccess) PublishCore();
        else _dispatcher.TryEnqueue(PublishCore);
    }

    private void PublishTransitions(
        IReadOnlyList<DeviceHubDevice> previous,
        IReadOnlyList<DeviceHubDevice> next)
    {
        var previousById = previous.ToDictionary(device => device.Id, StringComparer.Ordinal);
        var nextById = next.ToDictionary(device => device.Id, StringComparer.Ordinal);
        foreach (var device in next)
        {
            if (!previousById.TryGetValue(device.Id, out var old) &&
                device.ConnectionState == DeviceHubConnectionState.Connected)
            {
                _batteryWarningLevels.Remove(device.Id);
                RaiseDeduplicated(DeviceHubChangeKind.Connected, device);
            }
            else if (previousById.TryGetValue(device.Id, out old) &&
                     old.ConnectionState != device.ConnectionState)
            {
                if (device.ConnectionState == DeviceHubConnectionState.Connected ||
                    device.ConnectionState == DeviceHubConnectionState.Disconnected)
                {
                    _batteryWarningLevels.Remove(device.Id);
                }
                RaiseDeduplicated(
                    device.ConnectionState == DeviceHubConnectionState.Connected
                        ? DeviceHubChangeKind.Connected
                        : DeviceHubChangeKind.Disconnected,
                    device,
                    old);
            }
        }

        foreach (var device in previous)
        {
            if (!nextById.ContainsKey(device.Id) && device.ConnectionState == DeviceHubConnectionState.Connected)
            {
                _batteryWarningLevels.Remove(device.Id);
                RaiseDeduplicated(DeviceHubChangeKind.Disconnected,
                    device with { ConnectionState = DeviceHubConnectionState.Disconnected }, device);
            }
        }
    }

    private void PublishDefaultOutputChange(
        IReadOnlyList<DeviceHubDevice> previous,
        IReadOnlyList<DeviceHubDevice> next)
    {
        var oldDefault = previous.FirstOrDefault(device => device.IsDefault);
        var newDefault = next.FirstOrDefault(device => device.IsDefault);
        if (newDefault is not null && !string.Equals(oldDefault?.Id, newDefault.Id, StringComparison.Ordinal))
        {
            RaiseDeduplicated(DeviceHubChangeKind.DefaultAudioOutputChanged, newDefault, oldDefault);
        }
    }

    private void PublishBatteryWarnings(IReadOnlyList<DeviceHubDevice> devices)
    {
        if (!_settings.Current.ShowBatteryWarnings) return;
        foreach (var device in devices)
        {
            if (device.ConnectionState != DeviceHubConnectionState.Connected)
            {
                continue;
            }
            var battery = device.BatteryPercentage;
            if (battery is not >= 0 and <= 100)
            {
                continue;
            }

            var threshold = new[]
                {
                    _settings.Current.BatteryWarningPercent,
                    Math.Max(5, _settings.Current.BatteryWarningPercent / 2),
                    5
                }
                .Distinct()
                .OrderBy(level => level)
                .FirstOrDefault(level => battery <= level);
            if (threshold == 0)
            {
                _batteryWarningLevels.Remove(device.Id);
                continue;
            }

            if (!_batteryWarningLevels.TryGetValue(device.Id, out var previous) || threshold < previous)
            {
                _batteryWarningLevels[device.Id] = threshold;
                RaiseDeduplicated(DeviceHubChangeKind.BatteryLow, device);
            }
        }
    }

    private void RaiseDeduplicated(
        DeviceHubChangeKind kind,
        DeviceHubDevice device,
        DeviceHubDevice? previous = null)
    {
        if (device.Category == DeviceHubDeviceCategory.RemovableStorage &&
            kind is DeviceHubChangeKind.Connected or DeviceHubChangeKind.Disconnected &&
            !_settings.Current.ShowStorageEvents)
        {
            return;
        }

        if ((kind == DeviceHubChangeKind.Connected && !_settings.Current.ShowConnectedEvents) ||
            (kind == DeviceHubChangeKind.Disconnected && !_settings.Current.ShowDisconnectedEvents) ||
            (kind == DeviceHubChangeKind.DefaultAudioOutputChanged && !_settings.Current.ShowAudioOutputEvents) ||
            (kind == DeviceHubChangeKind.BatteryLow && !_settings.Current.ShowBatteryWarnings))
        {
            return;
        }
        var shouldDeduplicate = kind is DeviceHubChangeKind.Connected
            or DeviceHubChangeKind.Disconnected
            or DeviceHubChangeKind.DefaultAudioOutputChanged;
        var key = $"{kind}:{device.Id}";
        var now = DateTimeOffset.UtcNow;
        if (shouldDeduplicate &&
            _deduplication.TryGetValue(key, out var last) &&
            now - last < TimeSpan.FromSeconds(2))
        {
            return;
        }

        if (shouldDeduplicate)
        {
            _deduplication[key] = now;
        }
        DeviceChanged?.Invoke(this, new DeviceHubChange(kind, device, previous));
    }

    private async Task<IReadOnlyList<AudioDeviceInfo>> GetOutputsSafelyAsync(CancellationToken cancellationToken)
    {
        try { return await _audioDevices.GetOutputDevicesAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) { LogProviderFailure("audio-output", exception); return []; }
    }

    private async Task<IReadOnlyList<AudioDeviceInfo>> GetInputsSafelyAsync(CancellationToken cancellationToken)
    {
        try { return await _audioDevices.GetInputDevicesAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) { LogProviderFailure("audio-input", exception); return []; }
    }

    private async Task<IReadOnlyList<RemovableStorageInfo>> GetStorageSafelyAsync(CancellationToken cancellationToken)
    {
        try { return await _storage.GetDevicesAsync(cancellationToken).ConfigureAwait(false); }
        catch (Exception exception) { LogProviderFailure("storage", exception); return []; }
    }

    private void LogProviderFailure(string provider, Exception exception) =>
        _log?.Write(TechnicalLogLevel.Warning, TechnicalEventIds.DeviceStatusUnavailable,
            "DeviceHub", "A Device Hub provider failed safely.", exception,
            new Dictionary<string, object?> { ["provider"] = provider });

    private void PublishState(DeviceHubState state)
    {
        void PublishCore()
        {
            Current = state;
            StateChanged?.Invoke(this, state);
        }

        if (_dispatcher is null || _dispatcher.HasThreadAccess) PublishCore();
        else _dispatcher.TryEnqueue(PublishCore);
    }

    private static DeviceHubDeviceCapabilities BluetoothCapabilities(BluetoothDeviceState device)
    {
        var capabilities = DeviceHubDeviceCapabilities.ManageInSettings;
        if (device.BatteryPercentage is >= 0 and <= 100)
            capabilities |= DeviceHubDeviceCapabilities.HasBattery;
        if (string.IsNullOrWhiteSpace(device.EndpointId) && string.IsNullOrWhiteSpace(device.DeviceAddress))
            return capabilities;
        return capabilities | (device.IsConnected
            ? DeviceHubDeviceCapabilities.Disconnect
            : DeviceHubDeviceCapabilities.Connect);
    }

    private static string FormatBytes(long bytes)
    {
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1) { value /= 1024; unit++; }
        return $"{value:0.#} {units[unit]}";
    }

    private string DeviceTypeText(DeviceHubDeviceType type) => type switch
    {
        DeviceHubDeviceType.Headphones => Text("DeviceHub.Type.Headphones", "Headphones"),
        DeviceHubDeviceType.Headset => Text("DeviceHub.Type.Headset", "Headset"),
        DeviceHubDeviceType.Speaker => Text("DeviceHub.Type.Speaker", "Speaker"),
        DeviceHubDeviceType.Mouse => Text("DeviceHub.Type.Mouse", "Mouse"),
        DeviceHubDeviceType.Keyboard => Text("DeviceHub.Type.Keyboard", "Keyboard"),
        DeviceHubDeviceType.Gamepad => Text("DeviceHub.Type.Gamepad", "Gamepad"),
        _ => Text("DeviceHub.Type.Other", "Other device")
    };

    private string Text(string key, string fallback, params object?[] arguments)
    {
        var localized = _localization?.Get(key);
        var format = localized is not null && localized != key ? localized : fallback;
        return arguments.Length == 0 ? format : string.Format(format, arguments);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _refreshLock.Dispose();
    }
}
