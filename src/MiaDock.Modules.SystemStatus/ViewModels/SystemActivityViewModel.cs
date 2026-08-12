using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiaDock.Core.Localization;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.Services;

namespace MiaDock.Modules.SystemStatus.ViewModels;

public sealed partial class SystemActivityViewModel : ObservableObject, IDisposable
{
    private readonly ISystemActivityService _service;
    private readonly ILocalizationService? _localization;
    private readonly IPrivacySettingsLauncher? _privacySettingsLauncher;

    public SystemActivityViewModel(
        ISystemActivityService service,
        ILocalizationService? localization = null,
        IPrivacySettingsLauncher? privacySettingsLauncher = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _localization = localization;
        _privacySettingsLauncher = privacySettingsLauncher;
        Snapshot = service.Current;
        service.SnapshotChanged += OnSnapshotChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged += OnLanguageChanged;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MasterVolumeText))]
    [NotifyPropertyChangedFor(nameof(ApplicationVolumeText))]
    [NotifyPropertyChangedFor(nameof(IsApplicationVolumeAvailable))]
    [NotifyPropertyChangedFor(nameof(MicrophoneText))]
    [NotifyPropertyChangedFor(nameof(CameraText))]
    [NotifyPropertyChangedFor(nameof(IsCameraInUse))]
    [NotifyPropertyChangedFor(nameof(CallText))]
    [NotifyPropertyChangedFor(nameof(MasterVolumeGlyph))]
    [NotifyPropertyChangedFor(nameof(ActivityTitle))]
    [NotifyPropertyChangedFor(nameof(ActivityDetail))]
    [NotifyPropertyChangedFor(nameof(ActivityGlyph))]
    public partial SystemActivitySnapshot Snapshot { get; set; }

    public string MasterVolumeText => Snapshot.IsMasterVolumeAvailable
        ? $"%{Snapshot.MasterVolumePercent}"
        : Text("System.Unavailable", "Kullanılamıyor");

    public string ApplicationVolumeText => Snapshot.ApplicationVolumeAvailability switch
    {
        ApplicationVolumeAvailability.Available => $"%{Snapshot.ApplicationVolumePercent}",
        ApplicationVolumeAvailability.NoSelectedApplication => Text("System.NoApplication", "Uygulama seçilmedi"),
        ApplicationVolumeAvailability.SessionNotFound => Text("System.SessionNotFound", "Ses oturumu bulunamadı"),
        _ => Text("System.Unavailable", "Kullanılamıyor")
    };

    public bool IsApplicationVolumeAvailable =>
        Snapshot.ApplicationVolumeAvailability == ApplicationVolumeAvailability.Available;

    public string MicrophoneText => Snapshot.MicrophoneUsage switch
    {
        MicrophoneUsageState.Active => Text("System.Microphone.Active", "Mikrofon etkin"),
        MicrophoneUsageState.Idle => Text("System.Microphone.Idle", "Mikrofon boşta"),
        _ => Text("System.Microphone.Unavailable", "Mikrofon kullanılamıyor")
    };

    public string CameraText => Snapshot.CameraDeviceAvailability switch
    {
        CameraDeviceAvailability.Unavailable => Text("System.Camera.Unavailable", "Kamera cihaz durumu kullanılamıyor"),
        CameraDeviceAvailability.NotFound => Text("System.Camera.NotFound", "Kamera bulunamadı"),
        _ => Snapshot.CameraAccess switch
        {
            CameraAccessState.Allowed => Text("System.Camera.Allowed", "Kamera mevcut · erişim izinli"),
            CameraAccessState.DeniedByUser => Text("System.Camera.DeniedUser", "Kamera mevcut · kullanıcı engelledi"),
            CameraAccessState.DeniedBySystem => Text("System.Camera.DeniedSystem", "Kamera mevcut · sistem engelledi"),
            CameraAccessState.PromptRequired => Text("System.Camera.Prompt", "Kamera mevcut · izin istenmedi"),
            CameraAccessState.NotDeclared => Text("System.Camera.NotDeclared", "Kamera mevcut · yetenek tanımlı değil"),
            _ => Text("System.Camera.Unknown", "Kamera mevcut · erişim bilinmiyor")
        }
    };

    public bool IsCameraInUse =>
        Snapshot.CameraDeviceAvailability == CameraDeviceAvailability.Available &&
        Snapshot.CallActivity == CallActivityState.Possible;

    public string CallText => Snapshot.CallActivity == CallActivityState.Possible
        ? Text("Dock.CallPossible", "Olası arama etkinliği")
        : Text("System.Call.None", "Arama algılanmadı");

    public string ActivityTitle => Snapshot.CallActivity == CallActivityState.Possible
        ? Text("Dock.CallPossible", "Olası arama etkinliği")
        : Text("System.Call.None", "Arama algılanmadı");

    public string ActivityDetail => Snapshot.CallActivity == CallActivityState.Possible
        ? Text("System.Call.Detail", "Mikrofon ve iletişim sesi etkin")
        : Text("System.Call.IdleDetail", "Yerel arama çıkarımı");

    public string ActivityGlyph => "\uE717";

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

    [RelayCommand]
    private Task OpenMicrophonePrivacySettingsAsync() =>
        _privacySettingsLauncher?.OpenMicrophonePrivacySettingsAsync() ?? Task.FromResult(false);

    [RelayCommand]
    private Task OpenCameraPrivacySettingsAsync() =>
        _privacySettingsLauncher?.OpenCameraPrivacySettingsAsync() ?? Task.FromResult(false);

    private void OnSnapshotChanged(object? sender, SystemActivitySnapshot snapshot) => Snapshot = snapshot;

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        OnPropertyChanged(nameof(MasterVolumeText));
        OnPropertyChanged(nameof(ApplicationVolumeText));
        OnPropertyChanged(nameof(MicrophoneText));
        OnPropertyChanged(nameof(CameraText));
        OnPropertyChanged(nameof(CallText));
        OnPropertyChanged(nameof(ActivityTitle));
        OnPropertyChanged(nameof(ActivityDetail));
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
