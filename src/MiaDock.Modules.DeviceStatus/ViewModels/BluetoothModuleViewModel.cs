using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.Core.Localization;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;

namespace MiaDock.Modules.DeviceStatus.ViewModels;

public sealed partial class BluetoothModuleViewModel : ObservableObject, IDisposable
{
    private readonly IBluetoothStatusService _service;
    private readonly ILocalizationService? _localization;

    public BluetoothModuleViewModel(IBluetoothStatusService service, ILocalizationService? localization = null)
    {
        _service = service;
        _localization = localization;
        _snapshot = service.Current;
        _service.SnapshotChanged += OnSnapshotChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged += OnLanguageChanged;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectedDevices))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private BluetoothStatusSnapshot _snapshot;

    public IReadOnlyList<BluetoothDeviceState> ConnectedDevices => Snapshot.Devices
        .Where(device => device.IsConnected)
        .OrderBy(device => device.DisplayName, StringComparer.CurrentCultureIgnoreCase)
        .ToArray();

    public string StatusText => Snapshot.State switch
    {
        DeviceServiceState.Unavailable => Text("Bluetooth.Unavailable", "Bluetooth kullanılamıyor"),
        DeviceServiceState.Faulted => Text("Bluetooth.Faulted", "Bluetooth izleme hatası"),
        _ when !Snapshot.IsEnumerationComplete => Text("Bluetooth.Searching", "Cihazlar aranıyor"),
        _ when ConnectedDevices.Count == 0 => Text("Bluetooth.None", "Bağlı cihaz yok"),
        _ => Text("Bluetooth.Count", "{0} cihaz bağlı", ConnectedDevices.Count)
    };

    private void OnSnapshotChanged(object? sender, BluetoothStatusSnapshot snapshot) => Snapshot = snapshot;

    private void OnLanguageChanged(object? sender, EventArgs args) =>
        OnPropertyChanged(nameof(StatusText));

    private string Text(string key, string fallback, params object?[] arguments)
    {
        var value = _localization?.Get(key, arguments);
        return value is not null && value != key
            ? value
            : string.Format(fallback, arguments);
    }

    public void Dispose()
    {
        _service.SnapshotChanged -= OnSnapshotChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }
    }
}
