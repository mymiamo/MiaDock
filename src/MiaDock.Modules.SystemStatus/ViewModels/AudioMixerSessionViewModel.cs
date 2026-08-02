using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiaDock.Core.Localization;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.Services;

namespace MiaDock.Modules.SystemStatus.ViewModels;

public sealed partial class AudioMixerSessionViewModel : ObservableObject
{
    private readonly IAudioMixerService _service;
    private readonly ILocalizationService? _localization;

    public AudioMixerSessionViewModel(
        AudioMixerSessionSnapshot snapshot,
        IAudioMixerService service,
        ILocalizationService? localization = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _localization = localization;
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(VolumeText))]
    [NotifyPropertyChangedFor(nameof(MuteGlyph))]
    [NotifyPropertyChangedFor(nameof(IconGlyph))]
    [NotifyPropertyChangedFor(nameof(SupportText))]
    [NotifyPropertyChangedFor(nameof(VolumeAutomationName))]
    [NotifyPropertyChangedFor(nameof(MuteAutomationName))]
    [NotifyPropertyChangedFor(nameof(MuteToolTip))]
    public partial AudioMixerSessionSnapshot Snapshot { get; set; }

    public string SessionKey => Snapshot.SessionKey;

    public string DisplayName
    {
        get
        {
            if (Snapshot.IsSystemSounds)
            {
                return Text("Mixer.SystemSounds", "Sistem sesleri");
            }

            var name = !string.IsNullOrWhiteSpace(Snapshot.DisplayName)
                ? Snapshot.DisplayName
                : Snapshot.ProcessName;
            if (string.IsNullOrWhiteSpace(name))
            {
                return Text("Mixer.UnknownSession", "Bilinmeyen ses oturumu");
            }

            return char.ToUpperInvariant(name[0]) + name[1..];
        }
    }

    public string VolumeText => Snapshot.CanControlVolume
        ? $"{Snapshot.VolumePercent}%"
        : "—";

    public string MuteGlyph => Snapshot.IsMuted ? "\uE74F" : "\uE995";

    public string IconGlyph => Snapshot.IsSystemSounds ? "\uE995" : "\uE8A5";

    public string SupportText => Snapshot.CanControlVolume
        ? Text("Mixer.SessionControllable", "Ses denetimi kullanılabilir")
        : Text("Mixer.SessionUnsupported", "Bu oturum denetlenemiyor");

    public string VolumeAutomationName =>
        Text("Mixer.SessionVolumeAutomation", "{0} ses seviyesi", DisplayName);

    public string MuteAutomationName => Snapshot.IsMuted
        ? Text("Mixer.SessionUnmuteAutomation", "{0} sesini aç", DisplayName)
        : Text("Mixer.SessionMuteAutomation", "{0} sesini kapat", DisplayName);

    public string MuteToolTip => MuteAutomationName;

    [RelayCommand(CanExecute = nameof(CanControlVolume))]
    private Task ToggleMuteAsync() =>
        _service.ToggleSessionMuteAsync(SessionKey);

    private bool CanControlVolume() => Snapshot.CanControlVolume;

    public Task<bool> SetVolumeAsync(double percent) =>
        Snapshot.CanControlVolume
            ? _service.SetSessionVolumeAsync(
                SessionKey,
                Math.Clamp(percent / 100, 0, 1))
            : Task.FromResult(false);

    public void Refresh(
        AudioMixerSessionSnapshot snapshot,
        bool languageChanged = false)
    {
        Snapshot = snapshot;
        ToggleMuteCommand.NotifyCanExecuteChanged();
        if (languageChanged)
        {
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(SupportText));
            OnPropertyChanged(nameof(VolumeAutomationName));
            OnPropertyChanged(nameof(MuteAutomationName));
            OnPropertyChanged(nameof(MuteToolTip));
        }
    }

    private string Text(string key, string fallback, params object?[] arguments)
    {
        var value = _localization?.Get(key, arguments);
        return value is not null && value != key
            ? value
            : string.Format(fallback, arguments);
    }
}
