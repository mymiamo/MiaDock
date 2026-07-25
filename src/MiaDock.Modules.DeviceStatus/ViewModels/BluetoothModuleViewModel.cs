using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;

namespace MiaDock.Modules.DeviceStatus.ViewModels;

public sealed partial class BluetoothModuleViewModel : ObservableObject, IDisposable
{
    private readonly IBluetoothStatusService _service;

    public BluetoothModuleViewModel(IBluetoothStatusService service)
    {
        _service = service;
        _snapshot = service.Current;
        _service.SnapshotChanged += OnSnapshotChanged;
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
        DeviceServiceState.Unavailable => "Bluetooth kullanılamıyor",
        DeviceServiceState.Faulted => "Bluetooth izleme hatası",
        _ when !Snapshot.IsEnumerationComplete => "Cihazlar aranıyor",
        _ when ConnectedDevices.Count == 0 => "Bağlı cihaz yok",
        _ => $"{ConnectedDevices.Count} cihaz bağlı"
    };

    private void OnSnapshotChanged(object? sender, BluetoothStatusSnapshot snapshot) => Snapshot = snapshot;

    public void Dispose() => _service.SnapshotChanged -= OnSnapshotChanged;
}
