using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.Core.Localization;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;

namespace MiaDock.Modules.DeviceStatus.ViewModels;

public sealed partial class BatteryModuleViewModel : ObservableObject, IDisposable
{
    private readonly IPowerStatusService _service;
    private readonly ILocalizationService? _localization;

    public BatteryModuleViewModel(IPowerStatusService service, ILocalizationService? localization = null)
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
    [NotifyPropertyChangedFor(nameof(ChargeText))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(PowerText))]
    [NotifyPropertyChangedFor(nameof(BatteryGlyph))]
    private BatteryStatusSnapshot _snapshot;

    public string ChargeText => Snapshot.IsBatteryPresent
        ? $"%{Snapshot.ChargePercent}"
        : Text("Battery.None", "Pil yok");

    public string StatusText => !Snapshot.IsBatteryPresent
        ? Text("Battery.NotDetected", "Bu cihazda pil algılanmadı")
        : Snapshot.IsCharging
            ? Text("Battery.Charging", "Şarj oluyor")
            : Snapshot.IsEnergySaverOn
                ? Text("Battery.EnergySaver", "Enerji tasarrufu açık")
                : Text("Battery.OnBattery", "Pilde çalışıyor");

    public string PowerText => Snapshot.IsBatteryPresent
        ? Snapshot.PowerSource switch
        {
            "AC" => Text("Battery.PowerSource.AC", "Şebeke gücü"),
            "DC" => Text("Battery.PowerSource.DC", "Pil gücü"),
            "USB" => Text("Battery.PowerSource.USB", "USB gücü"),
            _ => Text("Battery.PowerSource.Unknown", "Güç kaynağı bilinmiyor")
        }
        : Text("Battery.DesktopPower", "Masaüstü güç sistemi");

    public string BatteryGlyph => Snapshot.IsCharging ? "\uE83E" : "\uE850";

    private void OnSnapshotChanged(object? sender, BatteryStatusSnapshot snapshot) => Snapshot = snapshot;

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        OnPropertyChanged(nameof(ChargeText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(PowerText));
    }

    private string Text(string key, string fallback) =>
        _localization?.Get(key) is { } value && value != key ? value : fallback;

    public void Dispose()
    {
        _service.SnapshotChanged -= OnSnapshotChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }
    }
}
