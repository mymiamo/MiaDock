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
    private readonly ILocalizationService? _localization;

    public DeviceHubViewModel(
        IDeviceHubService service,
        IRemovableStorageService storage,
        IDeviceHubSettingsLauncher settingsLauncher,
        ILocalizationService? localization = null)
    {
        _service = service;
        _storage = storage;
        _settingsLauncher = settingsLauncher;
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

    [ObservableProperty]
    private bool _storageOperationOpen;

    [ObservableProperty]
    private bool _storageOperationError;

    [ObservableProperty]
    private string _storageOperationMessage = string.Empty;

    [RelayCommand]
    private Task OpenBluetoothSettingsAsync() => _settingsLauncher.OpenBluetoothSettingsAsync();

    public Task OpenBluetoothSettingsPageAsync() => _settingsLauncher.OpenBluetoothSettingsAsync();

    [RelayCommand]
    private Task OpenSoundSettingsAsync() => _settingsLauncher.OpenSoundSettingsAsync();

    public Task OpenSoundSettingsPageAsync() => _settingsLauncher.OpenSoundSettingsAsync();

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
