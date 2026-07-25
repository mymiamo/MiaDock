using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;

namespace MiaDock.Modules.DeviceStatus.ViewModels;

public sealed partial class BatteryModuleViewModel : ObservableObject, IDisposable
{
    private readonly IPowerStatusService _service;

    public BatteryModuleViewModel(IPowerStatusService service)
    {
        _service = service;
        _snapshot = service.Current;
        _service.SnapshotChanged += OnSnapshotChanged;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChargeText))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(PowerText))]
    [NotifyPropertyChangedFor(nameof(BatteryGlyph))]
    private BatteryStatusSnapshot _snapshot;

    public string ChargeText => Snapshot.IsBatteryPresent ? $"%{Snapshot.ChargePercent}" : "Pil yok";

    public string StatusText => !Snapshot.IsBatteryPresent
        ? "Bu cihazda pil algılanmadı"
        : Snapshot.IsCharging
            ? "Şarj oluyor"
            : Snapshot.IsEnergySaverOn ? "Enerji tasarrufu açık" : "Pilde çalışıyor";

    public string PowerText => Snapshot.IsBatteryPresent ? Snapshot.PowerSource : "Masaüstü güç sistemi";

    public string BatteryGlyph => Snapshot.IsCharging ? "\uE83E" : "\uE850";

    private void OnSnapshotChanged(object? sender, BatteryStatusSnapshot snapshot) => Snapshot = snapshot;

    public void Dispose() => _service.SnapshotChanged -= OnSnapshotChanged;
}
