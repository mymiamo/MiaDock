using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiaDock.Core.Localization;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;

namespace MiaDock.Modules.DeviceStatus.ViewModels;

public sealed partial class DeviceHubViewModel : ObservableObject, IDisposable
{
    private readonly IDeviceHubService _service;
    private readonly IRemovableStorageService _storage;
    private readonly IDeviceHubSettingsLauncher _settingsLauncher;
    private readonly IBluetoothDeviceConnectionService? _bluetoothConnection;
    private readonly ILocalizationService? _localization;
    private int _bluetoothBusy;

    public DeviceHubViewModel(
        IDeviceHubService service,
        IRemovableStorageService storage,
        IDeviceHubSettingsLauncher settingsLauncher,
        IBluetoothDeviceConnectionService? bluetoothConnection = null,
        ILocalizationService? localization = null)
    {
        _service = service;
        _storage = storage;
        _settingsLauncher = settingsLauncher;
        _bluetoothConnection = bluetoothConnection;
        _localization = localization;
        _state = service.Current;
        service.StateChanged += OnStateChanged;
        if (localization is not null) localization.LanguageChanged += OnLanguageChanged;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(BluetoothDevices))]
    [NotifyPropertyChangedFor(nameof(AudioOutputDevices))]
    [NotifyPropertyChangedFor(nameof(AudioInputDevices))]
    [NotifyPropertyChangedFor(nameof(StorageDevices))]
    [NotifyPropertyChangedFor(nameof(BatteryDevices))]
    private DeviceHubState _state;

    public IReadOnlyList<DeviceHubDevice> BluetoothDevices => State.BluetoothDevices;
    public IReadOnlyList<DeviceHubDevice> AudioOutputDevices => State.AudioOutputDevices;
    public IReadOnlyList<DeviceHubDevice> AudioInputDevices => State.AudioInputDevices;
    public IReadOnlyList<DeviceHubDevice> StorageDevices => State.StorageDevices;
    public IReadOnlyList<DeviceHubDevice> BatteryDevices => State.BatteryDevices;
    public int ConnectedDeviceCount => State.BluetoothDevices.Count(device =>
        device.ConnectionState == DeviceHubConnectionState.Connected) + State.StorageDevices.Count;
    public string CompactSummary => Text(
        "DeviceHub.CompactSummary",
        "{0} connected · {1} audio outputs",
        ConnectedDeviceCount,
        State.AudioOutputDevices.Count);
    public string StatusText => State.State switch
    {
        DeviceServiceState.Faulted => Text("DeviceHub.Unavailable", "Cihaz bilgileri şu anda kullanılamıyor."),
        _ when State.IsInitialSnapshot => Text("DeviceHub.Loading", "Cihazlar hazırlanıyor."),
        _ => Text("DeviceHub.Ready", "Bağlı cihazlar güncel.")
    };
    public string ConnectText => Text("DeviceHub.Connect", "Connect");
    public string DisconnectText => Text("DeviceHub.Disconnect", "Disconnect");
    public string ManageBluetoothText => Text("DeviceHub.ManageBluetooth", "Manage in Bluetooth settings");

    [ObservableProperty]
    private bool _storageOperationOpen;

    [ObservableProperty]
    private bool _storageOperationError;

    [ObservableProperty]
    private string _storageOperationMessage = string.Empty;

    [ObservableProperty]
    private bool _bluetoothOperationOpen;

    [ObservableProperty]
    private bool _bluetoothOperationError;

    [ObservableProperty]
    private string _bluetoothOperationMessage = string.Empty;

    [RelayCommand]
    private Task OpenBluetoothSettingsAsync() => _settingsLauncher.OpenBluetoothSettingsAsync();

    public Task OpenBluetoothSettingsPageAsync() => _settingsLauncher.OpenBluetoothSettingsAsync();

    [RelayCommand]
    private Task OpenSoundSettingsAsync() => _settingsLauncher.OpenSoundSettingsAsync();

    public Task OpenSoundSettingsPageAsync() => _settingsLauncher.OpenSoundSettingsAsync();

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task ConnectBluetoothAsync(DeviceHubDevice? device) =>
        ChangeBluetoothAsync(device, connect: true);

    public Task ConnectBluetoothDeviceAsync(DeviceHubDevice? device) =>
        ChangeBluetoothAsync(device, connect: true);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task DisconnectBluetoothAsync(DeviceHubDevice? device) =>
        ChangeBluetoothAsync(device, connect: false);

    public Task DisconnectBluetoothDeviceAsync(DeviceHubDevice? device) =>
        ChangeBluetoothAsync(device, connect: false);

    [RelayCommand]
    private async Task OpenStorageAsync(DeviceHubDevice? device)
    {
        if (device is not { Category: DeviceHubDeviceCategory.RemovableStorage } ||
            string.IsNullOrWhiteSpace(device.NativeDeviceId)) return;
        await _storage.OpenAsync(new RemovableStorageInfo(
            device.Id, device.DisplayName, device.NativeDeviceId, device.FileSystem,
            device.TotalSpace, device.FreeSpace,
            device.ConnectionState == DeviceHubConnectionState.Connected,
            device.DeviceInstanceId,
            device.CanEject));
    }

    public Task OpenStorageDeviceAsync(DeviceHubDevice? device) => OpenStorageAsync(device);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task EjectStorageAsync(DeviceHubDevice? device)
    {
        if (device is not { Category: DeviceHubDeviceCategory.RemovableStorage } ||
            string.IsNullOrWhiteSpace(device.NativeDeviceId))
        {
            return;
        }

        StorageOperationOpen = false;
        var storage = new RemovableStorageInfo(
            device.Id,
            device.DisplayName,
            device.NativeDeviceId,
            device.FileSystem,
            device.TotalSpace,
            device.FreeSpace,
            device.ConnectionState == DeviceHubConnectionState.Connected,
            device.DeviceInstanceId,
            device.CanEject);
        var result = await _storage.EjectAsync(storage);
        StorageOperationError = !result.Succeeded;
        StorageOperationMessage = result.Status switch
        {
            RemovableStorageEjectStatus.Succeeded => Text("DeviceHub.EjectSucceeded", "Safe to remove {0}.", device.DisplayName),
            RemovableStorageEjectStatus.InUse => Text("DeviceHub.EjectInUse", "{0} is in use. Close open files and try again.", device.DisplayName),
            RemovableStorageEjectStatus.AccessDenied => Text("DeviceHub.EjectAccessDenied", "Windows did not allow {0} to be removed.", device.DisplayName),
            RemovableStorageEjectStatus.NotFound => Text("DeviceHub.EjectNotFound", "{0} is no longer available.", device.DisplayName),
            RemovableStorageEjectStatus.Unsupported => Text("DeviceHub.EjectUnsupported", "Use Windows settings to remove {0} safely.", device.DisplayName),
            _ => Text("DeviceHub.EjectFailed", "{0} could not be removed safely.", device.DisplayName)
        };
        StorageOperationOpen = true;
        if (result.Succeeded)
        {
            _service.NotifySafeToRemove(device);
            await _service.RefreshAsync();
        }
    }

    [RelayCommand]
    private Task OpenSafelyRemoveHardwareAsync() => _storage.OpenSafelyRemoveHardwareAsync();

    private async Task ChangeBluetoothAsync(DeviceHubDevice? device, bool connect)
    {
        if (_bluetoothConnection is null ||
            device is not { Category: DeviceHubDeviceCategory.Bluetooth })
            return;
        if (Interlocked.CompareExchange(ref _bluetoothBusy, 1, 0) != 0)
            return;

        try
        {
            BluetoothOperationOpen = false;
            var request = new BluetoothConnectionRequest(device.NativeDeviceId, device.DeviceAddress, device.DeviceType);
            var result = connect
                ? await _bluetoothConnection.ConnectAsync(request)
                : await _bluetoothConnection.DisconnectAsync(request);
            BluetoothOperationError = result != BluetoothConnectionResult.Succeeded;
            BluetoothOperationMessage = result switch
            {
                BluetoothConnectionResult.Succeeded when connect =>
                    Text("DeviceHub.ConnectSucceeded", "Connected {0}.", device.DisplayName),
                BluetoothConnectionResult.Succeeded =>
                    Text("DeviceHub.DisconnectSucceeded", "Disconnected {0}.", device.DisplayName),
                BluetoothConnectionResult.RadioOff =>
                    Text("DeviceHub.ConnectRadioOff", "Turn Bluetooth on and try again."),
                BluetoothConnectionResult.Unavailable or BluetoothConnectionResult.AccessDenied =>
                    Text("DeviceHub.ConnectUnsupported", "Use Bluetooth settings to manage {0}.", device.DisplayName),
                _ when connect =>
                    Text("DeviceHub.ConnectFailed", "{0} could not be connected.", device.DisplayName),
                _ =>
                    Text("DeviceHub.DisconnectFailed", "{0} could not be disconnected.", device.DisplayName)
            };
            BluetoothOperationOpen = true;
            if (result == BluetoothConnectionResult.Succeeded)
                await _service.RefreshAsync();
        }
        finally
        {
            Interlocked.Exchange(ref _bluetoothBusy, 0);
        }
    }

    private void OnStateChanged(object? sender, DeviceHubState state)
    {
        State = state;
        OnPropertyChanged(nameof(ConnectedDeviceCount));
        OnPropertyChanged(nameof(CompactSummary));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CompactSummary));
        OnPropertyChanged(nameof(ConnectText));
        OnPropertyChanged(nameof(DisconnectText));
        OnPropertyChanged(nameof(ManageBluetoothText));
        _ = _service.RefreshAsync();
    }

    private string Text(string key, string fallback, params object?[] arguments)
    {
        var localized = _localization?.Get(key);
        var format = localized is not null && localized != key ? localized : fallback;
        return arguments.Length == 0 ? format : string.Format(format, arguments);
    }

    public void Dispose()
    {
        _service.StateChanged -= OnStateChanged;
        if (_localization is not null) _localization.LanguageChanged -= OnLanguageChanged;
    }
}
