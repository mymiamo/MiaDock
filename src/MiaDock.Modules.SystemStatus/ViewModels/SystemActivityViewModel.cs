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
    [NotifyPropertyChangedFor(nameof(CallText))]
    [NotifyPropertyChangedFor(nameof(MasterVolumeGlyph))]
    [NotifyPropertyChangedFor(nameof(ActivityTitle))]
    [NotifyPropertyChangedFor(nameof(ActivityDetail))]
    [NotifyPropertyChangedFor(nameof(ActivityGlyph))]
    [NotifyPropertyChangedFor(nameof(IsCallIdle))]
    [NotifyPropertyChangedFor(nameof(IsCallPossible))]
    [NotifyPropertyChangedFor(nameof(IsCallDetected))]
    [NotifyPropertyChangedFor(nameof(CallAppName))]
    [NotifyPropertyChangedFor(nameof(CallStatusBadgeText))]
    [NotifyPropertyChangedFor(nameof(CompactTrailingText))]
    [NotifyPropertyChangedFor(nameof(PossibleDetail))]
    [NotifyPropertyChangedFor(nameof(PresentationVisualKey))]
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

    public bool IsCallIdle => Snapshot.CallActivity.State == CallActivityState.None;
    public bool IsCallPossible => Snapshot.CallActivity.State == CallActivityState.Possible;
    public bool IsCallDetected => Snapshot.CallActivity.State == CallActivityState.Detected;
    public string CallAppName => Snapshot.CallActivity.DisplayName ?? Text("System.Call.UnknownApp", "Communication app");

    public string CallText => Snapshot.CallActivity.State switch
    {
        CallActivityState.Detected => Text("System.Call.Detected", "Call detected"),
        CallActivityState.Possible => Text("Dock.CallPossible", "Possible call activity"),
        _ => Text("System.Call.None", "No call detected")
    };

    public string ActivityTitle => Snapshot.CallActivity.State switch
    {
        CallActivityState.Detected => Format("System.Call.AppTitle", "{0} call", CallAppName),
        CallActivityState.Possible => Text("Dock.CallPossible", "Possible call activity"),
        _ => Text("System.Call.None", "No call detected")
    };

    public string ActivityDetail => Snapshot.CallActivity.State switch
    {
        CallActivityState.Detected => Text("System.Call.Detected", "Call detected"),
        CallActivityState.Possible => PossibleDetail,
        _ => Text("System.Call.IdleDetail", "Local detection · Audio is not recorded")
    };

    public string ActivityGlyph => Snapshot.CallActivity.App switch
    {
        CommunicationApp.Discord => "\uE8BD",
        CommunicationApp.Skype => "\uE714",
        CommunicationApp.WhatsApp => "\uE717",
        _ => "\uE717"
    };

    public string CallStatusBadgeText => Snapshot.CallActivity.State switch
    {
        CallActivityState.Detected => CallAppName,
        CallActivityState.Possible => Text("System.Call.PossibleBadge", "Possible activity"),
        _ => Text("System.Call.Waiting", "Waiting")
    };

    public string CompactTrailingText => IsCallDetected
        ? Text("System.Call.Active", "Active")
        : string.Empty;

    public string PossibleDetail => Snapshot.CallActivity.App == CommunicationApp.Unknown
        ? Text("System.Call.PossibleDetail", "Communication microphone activity was found, but a call could not be verified.")
        : Format(
            "System.Call.PossibleAppDetail",
            "{0} microphone activity was found, but a call could not be verified.",
            CallAppName);

    public string PresentationVisualKey =>
        $"{Snapshot.CallActivity.State}|{Snapshot.CallActivity.App}|{Snapshot.CallActivity.StartedAt:O}";
    public string CallAutomationText => Text("System.Call.Automation", "Call activity");
    public string LocalDetectionText => Text("System.Call.LocalDetection", "Local detection");
    public string PrivacyText => Text("System.Call.Privacy", "Audio is not recorded or sent.");
    public string IdleDescriptionText => Text(
        "System.Call.IdleDescription",
        "Discord, Skype, and WhatsApp calls can be detected locally.");
    public string SupportedAppsText => Text("System.Call.SupportedApps", "Supported apps");
    public string SupportedText => Text("System.Call.Supported", "Supported");
    public string DiscordName => "Discord";
    public string SkypeName => "Skype";
    public string WhatsAppName => "WhatsApp";
    public string CallInformationText => Text("System.Call.Information", "Call information");
    public string ApplicationText => Text("System.Call.Application", "Application");
    public string MicrophoneLabelText => Text("System.Call.Microphone", "Microphone");
    public string AudioSessionLabelText => Text("System.Call.AudioSession", "Audio session");
    public string ActiveText => Text("System.Call.Active", "Active");

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
        OnPropertyChanged(nameof(CallAppName));
        OnPropertyChanged(nameof(CallStatusBadgeText));
        OnPropertyChanged(nameof(CompactTrailingText));
        OnPropertyChanged(nameof(PossibleDetail));
        OnPropertyChanged(nameof(CallAutomationText));
        OnPropertyChanged(nameof(LocalDetectionText));
        OnPropertyChanged(nameof(PrivacyText));
        OnPropertyChanged(nameof(IdleDescriptionText));
        OnPropertyChanged(nameof(SupportedAppsText));
        OnPropertyChanged(nameof(SupportedText));
        OnPropertyChanged(nameof(CallInformationText));
        OnPropertyChanged(nameof(ApplicationText));
        OnPropertyChanged(nameof(MicrophoneLabelText));
        OnPropertyChanged(nameof(AudioSessionLabelText));
        OnPropertyChanged(nameof(ActiveText));
    }

    private string Text(string key, string fallback) =>
        _localization?.Get(key) is { } value && value != key ? value : fallback;

    private string Format(string key, string fallback, params object?[] arguments)
    {
        var localized = _localization?.Get(key);
        var format = localized is not null && localized != key ? localized : fallback;
        return string.Format(format, arguments);
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
