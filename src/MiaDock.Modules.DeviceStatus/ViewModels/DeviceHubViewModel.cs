using System.Collections.ObjectModel;
using System.Globalization;
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
        ReconcileRows();
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
    [NotifyPropertyChangedFor(nameof(HasAudioOutputs))]
    [NotifyPropertyChangedFor(nameof(HasAudioInputs))]
    [NotifyPropertyChangedFor(nameof(HasBluetoothDevices))]
    [NotifyPropertyChangedFor(nameof(HasStorageDevices))]
    [NotifyPropertyChangedFor(nameof(HasAnyDevices))]
    private DeviceHubState _state;

    public IReadOnlyList<DeviceHubDevice> BluetoothDevices => State.BluetoothDevices;
    public IReadOnlyList<DeviceHubDevice> AudioOutputDevices => State.AudioOutputDevices;
    public IReadOnlyList<DeviceHubDevice> AudioInputDevices => State.AudioInputDevices;
    public IReadOnlyList<DeviceHubDevice> StorageDevices => State.StorageDevices;
    public IReadOnlyList<DeviceHubDevice> BatteryDevices => State.BatteryDevices;
    public ObservableCollection<DeviceHubDeviceRow> AudioOutputRows { get; } = [];
    public ObservableCollection<DeviceHubDeviceRow> AudioInputRows { get; } = [];
    public ObservableCollection<DeviceHubDeviceRow> BluetoothRows { get; } = [];
    public ObservableCollection<DeviceHubDeviceRow> StorageRows { get; } = [];
    public bool HasAudioOutputs => AudioOutputRows.Count > 0;
    public bool HasAudioInputs => AudioInputRows.Count > 0;
    public bool HasBluetoothDevices => BluetoothRows.Count > 0;
    public bool HasStorageDevices => StorageRows.Count > 0;
    public bool HasAnyDevices => HasAudioOutputs || HasAudioInputs || HasBluetoothDevices || HasStorageDevices;
    public int ConnectedDeviceCount => State.BluetoothDevices.Count(device =>
        device.ConnectionState == DeviceHubConnectionState.Connected) + State.StorageDevices.Count;
    public string CompactSummary => Text(
        "DeviceHub.CompactSummary",
        "{0} connected · {1} audio outputs",
        ConnectedDeviceCount,
        State.AudioOutputDevices.Count);
    public string ConnectedSummary => ConnectedDeviceCount == 0
        ? string.Empty
        : Text("DeviceHub.ConnectedSummary", "{0} connected", ConnectedDeviceCount);
    public string StatusText => State.State switch
    {
        DeviceServiceState.Faulted => Text("DeviceHub.Unavailable", "Cihaz bilgileri şu anda kullanılamıyor."),
        _ when State.IsInitialSnapshot => Text("DeviceHub.Loading", "Cihazlar hazırlanıyor."),
        _ => Text("DeviceHub.Ready", "Bağlı cihazlar güncel.")
    };
    public string ConnectText => Text("DeviceHub.Connect", "Connect");
    public string DisconnectText => Text("DeviceHub.Disconnect", "Disconnect");
    public string ManageBluetoothText => Text("DeviceHub.ManageBluetooth", "Manage in Bluetooth settings");
    public string TitleText => Text("DeviceHub.Title", "Device Hub");
    public string AutomationText => Text("DeviceHub.Automation", "Connected devices");
    public string AudioOutputText => Text("DeviceHub.AudioOutput", "Audio output");
    public string AudioOutputAutomationText => Text("DeviceHub.AudioOutputAutomation", "Audio output devices");
    public string MicrophoneText => Text("DeviceHub.Microphone", "Microphone");
    public string MicrophoneAutomationText => Text("DeviceHub.MicrophoneAutomation", "Microphone devices");
    public string BluetoothText => Text("DeviceHub.Bluetooth", "Bluetooth");
    public string BluetoothAutomationText => Text("DeviceHub.BluetoothAutomation", "Bluetooth devices");
    public string StorageText => Text("DeviceHub.Storage", "Removable storage");
    public string StorageAutomationText => Text("DeviceHub.StorageAutomation", "Removable storage devices");
    public string EmptyText => Text("DeviceHub.Empty", "No connected devices found.");
    public string SelectSoundText => Text("DeviceHub.SelectSound", "Select in sound settings");
    public string WindowsRemoveOptionsText => Text("DeviceHub.WindowsRemoveOptions", "Windows remove options");
    public string OpenText => Text("DeviceHub.Open", "Open");
    public string EjectText => Text("DeviceHub.Eject", "Eject safely");
    public string CapacityText => Text("DeviceHub.Capacity", "Capacity");
    public string AvailableText => Text("DeviceHub.Available", "Available");
    public string FileSystemText => Text("DeviceHub.FileSystem", "File system");

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
        ReconcileRows();
        OnPropertyChanged(nameof(ConnectedDeviceCount));
        OnPropertyChanged(nameof(CompactSummary));
        OnPropertyChanged(nameof(ConnectedSummary));
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(CompactSummary));
        OnPropertyChanged(nameof(ConnectedSummary));
        OnPropertyChanged(nameof(ConnectText));
        OnPropertyChanged(nameof(DisconnectText));
        OnPropertyChanged(nameof(ManageBluetoothText));
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(AutomationText));
        OnPropertyChanged(nameof(AudioOutputText));
        OnPropertyChanged(nameof(AudioOutputAutomationText));
        OnPropertyChanged(nameof(MicrophoneText));
        OnPropertyChanged(nameof(MicrophoneAutomationText));
        OnPropertyChanged(nameof(BluetoothText));
        OnPropertyChanged(nameof(BluetoothAutomationText));
        OnPropertyChanged(nameof(StorageText));
        OnPropertyChanged(nameof(StorageAutomationText));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(SelectSoundText));
        OnPropertyChanged(nameof(WindowsRemoveOptionsText));
        OnPropertyChanged(nameof(OpenText));
        OnPropertyChanged(nameof(EjectText));
        OnPropertyChanged(nameof(CapacityText));
        OnPropertyChanged(nameof(AvailableText));
        OnPropertyChanged(nameof(FileSystemText));
        RefreshRowPresentations();
        _ = _service.RefreshAsync();
    }

    private void ReconcileRows()
    {
        Reconcile(AudioOutputRows, State.AudioOutputDevices);
        Reconcile(AudioInputRows, State.AudioInputDevices);
        Reconcile(BluetoothRows, State.BluetoothDevices);
        Reconcile(StorageRows, State.StorageDevices);
        OnPropertyChanged(nameof(HasAudioOutputs));
        OnPropertyChanged(nameof(HasAudioInputs));
        OnPropertyChanged(nameof(HasBluetoothDevices));
        OnPropertyChanged(nameof(HasStorageDevices));
        OnPropertyChanged(nameof(HasAnyDevices));
    }

    private static void Reconcile(
        ObservableCollection<DeviceHubDeviceRow> rows,
        IReadOnlyList<DeviceHubDevice> devices)
    {
        var desiredIds = devices.Select(device => device.Id).ToHashSet(StringComparer.Ordinal);
        for (var index = rows.Count - 1; index >= 0; index--)
        {
            if (!desiredIds.Contains(rows[index].Id))
            {
                rows.RemoveAt(index);
            }
        }

        for (var index = 0; index < devices.Count; index++)
        {
            var device = devices[index];
            var currentIndex = -1;
            for (var candidate = 0; candidate < rows.Count; candidate++)
            {
                if (string.Equals(rows[candidate].Id, device.Id, StringComparison.Ordinal))
                {
                    currentIndex = candidate;
                    break;
                }
            }

            if (currentIndex < 0)
            {
                rows.Insert(index, new DeviceHubDeviceRow(device, index == devices.Count - 1));
            }
            else
            {
                if (currentIndex != index)
                {
                    rows.Move(currentIndex, index);
                }
                rows[index].Update(device, index == devices.Count - 1);
            }
        }

        for (var index = 0; index < rows.Count; index++)
        {
            rows[index].SetLast(index == rows.Count - 1);
        }
    }

    private void RefreshRowPresentations()
    {
        foreach (var row in AudioOutputRows.Concat(AudioInputRows).Concat(BluetoothRows).Concat(StorageRows))
        {
            row.RefreshPresentation();
        }
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

public sealed class DeviceHubDeviceRow : ObservableObject
{
    private DeviceHubDevice _device;
    private bool _isLast;

    public DeviceHubDeviceRow(DeviceHubDevice device, bool isLast)
    {
        _device = device;
        _isLast = isLast;
    }

    public DeviceHubDevice Device => _device;
    public string Id => _device.Id;
    public string DisplayName => _device.DisplayName;
    public string? Detail => _device.Detail;
    public string? TrailingText => _device.Category switch
    {
        DeviceHubDeviceCategory.Bluetooth => _device.BatteryText,
        DeviceHubDeviceCategory.RemovableStorage => _device.FileSystem,
        _ => null
    };
    public string Glyph => _device.Category switch
    {
        DeviceHubDeviceCategory.AudioOutput when _device.DeviceType is DeviceHubDeviceType.Headphones or DeviceHubDeviceType.Headset => "\uE7F6",
        DeviceHubDeviceCategory.AudioOutput => "\uE767",
        DeviceHubDeviceCategory.AudioInput => "\uE720",
        DeviceHubDeviceCategory.RemovableStorage => "\uE88E",
        DeviceHubDeviceCategory.Bluetooth => _device.DeviceType switch
        {
            DeviceHubDeviceType.Headphones or DeviceHubDeviceType.Headset => "\uE7F6",
            DeviceHubDeviceType.Speaker => "\uE767",
            DeviceHubDeviceType.Keyboard => "\uE765",
            DeviceHubDeviceType.Mouse => "\uE962",
            DeviceHubDeviceType.Gamepad => "\uE7FC",
            _ => "\uE702"
        },
        _ => "\uE7F4"
    };
    public string AccessibilityText => string.Join(", ", new[] { DisplayName, Detail, TrailingText }
        .Where(value => !string.IsNullOrWhiteSpace(value)));
    public string VisualStateKey => $"{Id}|{_device.ConnectionState}|{_device.IsDefault}|{_device.BatteryPercentage}|{_device.FreeSpace}";
    public bool CanConnect => _device.CanConnect;
    public bool IsConnected => _device.ConnectionState == DeviceHubConnectionState.Connected;
    public bool CanDisconnect => _device.CanDisconnect;
    public bool CanOpen => _device.Capabilities.HasFlag(DeviceHubDeviceCapabilities.Open);
    public bool CanEject => _device.CanEject || _device.Capabilities.HasFlag(DeviceHubDeviceCapabilities.Eject);
    public bool HasDivider => !_isLast;
    public bool HasCapacity => _device.TotalSpace is >= 0;
    public bool HasAvailable => _device.FreeSpace is >= 0;
    public bool HasFileSystem => !string.IsNullOrWhiteSpace(_device.FileSystem);
    public string CapacityValue => FormatBytes(_device.TotalSpace);
    public string AvailableValue => FormatBytes(_device.FreeSpace);
    public string FileSystemValue => _device.FileSystem ?? string.Empty;

    internal void Update(DeviceHubDevice device, bool isLast)
    {
        var deviceChanged = _device != device;
        _device = device;
        _isLast = isLast;
        if (deviceChanged)
        {
            RefreshPresentation();
        }
        OnPropertyChanged(nameof(HasDivider));
    }

    internal void SetLast(bool isLast)
    {
        if (_isLast == isLast) return;
        _isLast = isLast;
        OnPropertyChanged(nameof(HasDivider));
    }

    internal void RefreshPresentation()
    {
        OnPropertyChanged(nameof(Device));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Detail));
        OnPropertyChanged(nameof(TrailingText));
        OnPropertyChanged(nameof(Glyph));
        OnPropertyChanged(nameof(AccessibilityText));
        OnPropertyChanged(nameof(VisualStateKey));
        OnPropertyChanged(nameof(CanConnect));
        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(CanDisconnect));
        OnPropertyChanged(nameof(CanOpen));
        OnPropertyChanged(nameof(CanEject));
        OnPropertyChanged(nameof(HasCapacity));
        OnPropertyChanged(nameof(HasAvailable));
        OnPropertyChanged(nameof(HasFileSystem));
        OnPropertyChanged(nameof(CapacityValue));
        OnPropertyChanged(nameof(AvailableValue));
        OnPropertyChanged(nameof(FileSystemValue));
    }

    private static string FormatBytes(long? bytes)
    {
        if (bytes is null or < 0) return string.Empty;
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes.Value;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return string.Format(CultureInfo.CurrentCulture, "{0:0.#} {1}", value, units[unit]);
    }
}
