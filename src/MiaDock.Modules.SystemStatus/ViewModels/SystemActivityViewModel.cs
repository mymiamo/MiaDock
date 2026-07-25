using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.Services;

namespace MiaDock.Modules.SystemStatus.ViewModels;

public sealed partial class SystemActivityViewModel : ObservableObject, IDisposable
{
    private readonly ISystemActivityService _service;

    public SystemActivityViewModel(ISystemActivityService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        Snapshot = service.Current;
        service.SnapshotChanged += OnSnapshotChanged;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MasterVolumeText))]
    [NotifyPropertyChangedFor(nameof(ApplicationVolumeText))]
    [NotifyPropertyChangedFor(nameof(IsApplicationVolumeAvailable))]
    [NotifyPropertyChangedFor(nameof(MicrophoneText))]
    [NotifyPropertyChangedFor(nameof(CameraText))]
    [NotifyPropertyChangedFor(nameof(CallText))]
    [NotifyPropertyChangedFor(nameof(MasterVolumeGlyph))]
    public partial SystemActivitySnapshot Snapshot { get; set; }

    public string MasterVolumeText => Snapshot.IsMasterVolumeAvailable
        ? $"%{Snapshot.MasterVolumePercent}"
        : "Kullanılamıyor";

    public string ApplicationVolumeText => Snapshot.ApplicationVolumeAvailability switch
    {
        ApplicationVolumeAvailability.Available => $"%{Snapshot.ApplicationVolumePercent}",
        ApplicationVolumeAvailability.NoSelectedApplication => "Uygulama seçilmedi",
        ApplicationVolumeAvailability.SessionNotFound => "Ses oturumu bulunamadı",
        _ => "Kullanılamıyor"
    };

    public bool IsApplicationVolumeAvailable =>
        Snapshot.ApplicationVolumeAvailability == ApplicationVolumeAvailability.Available;

    public string MicrophoneText => Snapshot.MicrophoneUsage switch
    {
        MicrophoneUsageState.Active => "Mikrofon etkin",
        MicrophoneUsageState.Idle => "Mikrofon boşta",
        _ => "Mikrofon kullanılamıyor"
    };

    public string CameraText => Snapshot.CameraDeviceAvailability switch
    {
        CameraDeviceAvailability.Unavailable => "Kamera cihaz durumu kullanılamıyor",
        CameraDeviceAvailability.NotFound => "Kamera bulunamadı",
        _ => Snapshot.CameraAccess switch
        {
            CameraAccessState.Allowed => "Kamera mevcut · erişim izinli",
            CameraAccessState.DeniedByUser => "Kamera mevcut · kullanıcı engelledi",
            CameraAccessState.DeniedBySystem => "Kamera mevcut · sistem engelledi",
            CameraAccessState.PromptRequired => "Kamera mevcut · izin istenmedi",
            CameraAccessState.NotDeclared => "Kamera mevcut · yetenek tanımlı değil",
            _ => "Kamera mevcut · erişim bilinmiyor"
        }
    };

    public string CallText => Snapshot.CallActivity == CallActivityState.Possible
        ? "Olası arama etkinliği"
        : "Arama algılanmadı";

    public string MasterVolumeGlyph => Snapshot.IsMasterMuted || Snapshot.MasterVolume <= 0
        ? "\uE74F"
        : Snapshot.MasterVolume < 0.5 ? "\uE993" : "\uE995";

    [RelayCommand]
    private Task ToggleMasterMuteAsync() => _service.ToggleMasterMuteAsync();

    [RelayCommand]
    private Task ToggleApplicationMuteAsync() => _service.ToggleApplicationMuteAsync();

    [RelayCommand]
    private Task DecreaseMasterVolumeAsync() =>
        _service.SetMasterVolumeAsync(Math.Max(0, Snapshot.MasterVolume - 0.05));

    [RelayCommand]
    private Task IncreaseMasterVolumeAsync() =>
        _service.SetMasterVolumeAsync(Math.Min(1, Snapshot.MasterVolume + 0.05));

    public Task SetMasterVolumeAsync(double percent) =>
        _service.SetMasterVolumeAsync(Math.Clamp(percent / 100, 0, 1));

    public Task SetApplicationVolumeAsync(double percent) =>
        _service.SetApplicationVolumeAsync(Math.Clamp(percent / 100, 0, 1));

    private void OnSnapshotChanged(object? sender, SystemActivitySnapshot snapshot) => Snapshot = snapshot;

    public void Dispose() => _service.SnapshotChanged -= OnSnapshotChanged;
}
