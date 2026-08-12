using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using MiaDock.Core.Localization;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.Services;
using MiaDock.Modules.SystemStatus.Settings;
using MiaDock.Core.Threading;

namespace MiaDock.Modules.SystemStatus.ViewModels;

public sealed partial class VolumeModuleViewModel : ObservableObject, IDisposable
{
    private readonly ISystemActivityService _service;
    private readonly IAudioSettingsLauncher _settingsLauncher;
    private readonly IVolumeModuleSettings _settings;
    private readonly ILocalizationService? _localization;
    private readonly IAudioMixerService? _mixer;
    private readonly IUiDispatcher? _uiDispatcher;
    private AudioMixerSnapshot _latestMixerSnapshot;
    private bool _mixerActive;
    private bool _disposed;

    public VolumeModuleViewModel(
        ISystemActivityService service,
        IAudioSettingsLauncher settingsLauncher,
        IVolumeModuleSettings settings,
        ILocalizationService? localization = null,
        IAudioMixerService? mixer = null,
        IUiDispatcher? uiDispatcher = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _settingsLauncher = settingsLauncher ?? throw new ArgumentNullException(nameof(settingsLauncher));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _localization = localization;
        _mixer = mixer;
        _uiDispatcher = uiDispatcher;
        Snapshot = service.Current;
        MixerSnapshot = mixer?.CurrentMixer ?? AudioMixerSnapshot.Default;
        _latestMixerSnapshot = MixerSnapshot;
        Options = settings.Current;
        service.SnapshotChanged += OnSnapshotChanged;
        settings.Changed += OnSettingsChanged;
        if (_mixer is not null)
        {
            _mixer.MixerChanged += OnMixerChanged;
            ReconcileMixerSessions(MixerSnapshot);
        }
        if (_localization is not null)
        {
            _localization.LanguageChanged += OnLanguageChanged;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VolumeText))]
    [NotifyPropertyChangedFor(nameof(VolumeGlyph))]
    [NotifyPropertyChangedFor(nameof(TitleText))]
    [NotifyPropertyChangedFor(nameof(OutputDeviceText))]
    [NotifyPropertyChangedFor(nameof(IsOutputDeviceVisible))]
    public partial SystemActivitySnapshot Snapshot { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputDeviceText))]
    [NotifyPropertyChangedFor(nameof(IsOutputDeviceVisible))]
    public partial VolumeModuleOptions Options { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMixerSessions))]
    [NotifyPropertyChangedFor(nameof(HasNoMixerSessions))]
    [NotifyPropertyChangedFor(nameof(MixerStatusText))]
    public partial AudioMixerSnapshot MixerSnapshot { get; set; }

    public ObservableCollection<AudioMixerSessionViewModel> MixerSessions { get; } = [];

    public bool HasMixerSessions => MixerSessions.Count > 0;
    public bool HasNoMixerSessions => !HasMixerSessions;

    public string MixerStatusText => MixerSnapshot.ServiceState switch
    {
        SystemActivityServiceState.Unavailable or SystemActivityServiceState.Faulted =>
            Text("Mixer.Unavailable", "Ses karıştırıcısı kullanılamıyor"),
        _ when MixerSessions.Count == 0 =>
            Text("Mixer.NoActiveSessions", "Etkin uygulama sesi yok"),
        _ => Text("Mixer.ActiveSessionCount", "{0} etkin ses uygulaması", MixerSessions.Count)
    };

    public string VolumeText => Snapshot.IsMasterVolumeAvailable
        ? $"{Snapshot.MasterVolumePercent}%"
        : Text("System.Unavailable", "Kullanılamıyor");

    public string VolumeGlyph => Snapshot.IsMasterMuted || Snapshot.MasterVolume <= 0
        ? "\uE74F"
        : Snapshot.MasterVolume < 0.5 ? "\uE993" : "\uE995";

    public string TitleText => Snapshot.IsMasterMuted
        ? Text("Volume.Muted", "Ses kapalı")
        : Text("Volume.Master", "Ana ses");

    public string OutputDeviceText => !string.IsNullOrWhiteSpace(Snapshot.DefaultOutputDeviceName)
        ? Snapshot.DefaultOutputDeviceName
        : Text("Volume.OutputDevice.Unavailable", "Çıkış aygıtı kullanılamıyor");

    public bool IsOutputDeviceVisible =>
        Options.ShowOutputDeviceName &&
        !string.IsNullOrWhiteSpace(Snapshot.DefaultOutputDeviceName);

    [RelayCommand]
    private Task ToggleMuteAsync() => _service.ToggleMasterMuteAsync();

    [RelayCommand]
    private Task OpenSoundSettingsFromUiAsync() => OpenSoundSettingsAsync();

    public Task<bool> OpenSoundSettingsAsync(CancellationToken cancellationToken = default) =>
        _settingsLauncher.OpenSoundSettingsAsync(cancellationToken);

    public Task SetMasterVolumeAsync(double percent) =>
        _service.SetMasterVolumeAsync(Math.Clamp(percent / 100, 0, 1));

    public void SetMixerActive(bool active)
    {
        if (_disposed || _mixer is null || _mixerActive == active)
        {
            return;
        }

        _mixerActive = active;
        _mixer.SetMeteringEnabled(active);
    }

    private void OnSnapshotChanged(object? sender, SystemActivitySnapshot snapshot)
        => DispatchToUi(() =>
        {
            if (!_disposed)
            {
                Snapshot = snapshot;
            }
        });

    private void OnSettingsChanged(object? sender, VolumeModuleOptions options)
        => DispatchToUi(() =>
        {
            if (!_disposed)
            {
                Options = options;
            }
        });

    private void OnMixerChanged(object? sender, AudioMixerSnapshot snapshot)
        => DispatchToUi(() => ApplyMixerSnapshot(snapshot));

    private void ApplyMixerSnapshot(AudioMixerSnapshot snapshot)
    {
        if (_disposed)
        {
            return;
        }

        _latestMixerSnapshot = snapshot;
        if (!MixerDetailsEqual(MixerSnapshot, snapshot))
        {
            MixerSnapshot = snapshot;
        }

        ReconcileMixerSessions(snapshot);
    }

    private void ReconcileMixerSessions(
        AudioMixerSnapshot snapshot,
        bool languageChanged = false)
    {
        var byKey = new Dictionary<string, AudioMixerSessionSnapshot>(
            StringComparer.Ordinal);
        foreach (var session in snapshot.Sessions)
        {
            if (!string.IsNullOrWhiteSpace(session.SessionKey))
            {
                byKey[session.SessionKey] = session;
            }
        }

        for (var index = MixerSessions.Count - 1; index >= 0; index--)
        {
            if (!byKey.ContainsKey(MixerSessions[index].SessionKey))
            {
                MixerSessions.RemoveAt(index);
            }
        }

        foreach (var session in byKey.Values)
        {
            var existing = MixerSessions.FirstOrDefault(item =>
                item.SessionKey == session.SessionKey);
            if (existing is null)
            {
                MixerSessions.Add(new AudioMixerSessionViewModel(
                    session,
                    _mixer!,
                    _localization));
            }
            else
            {
                existing.Refresh(session, languageChanged);
            }
        }

        OnPropertyChanged(nameof(HasMixerSessions));
        OnPropertyChanged(nameof(HasNoMixerSessions));
        OnPropertyChanged(nameof(MixerStatusText));
    }

    private void OnLanguageChanged(object? sender, EventArgs args)
        => DispatchToUi(ApplyLanguageChange);

    private void ApplyLanguageChange()
    {
        if (_disposed)
        {
            return;
        }

        OnPropertyChanged(nameof(VolumeText));
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(OutputDeviceText));
        ReconcileMixerSessions(_latestMixerSnapshot, languageChanged: true);
    }

    private static bool MixerDetailsEqual(
        AudioMixerSnapshot left,
        AudioMixerSnapshot right)
    {
        if (left.ServiceState != right.ServiceState ||
            left.OutputDeviceId != right.OutputDeviceId ||
            left.OutputDeviceName != right.OutputDeviceName ||
            left.IsMeteringEnabled != right.IsMeteringEnabled ||
            left.Sessions.Count != right.Sessions.Count)
        {
            return false;
        }

        return left.Sessions.All(session =>
            right.Sessions.Any(candidate =>
                string.Equals(
                    session.SessionKey,
                    candidate.SessionKey,
                    StringComparison.Ordinal) &&
                session with { PeakLevel = 0 } == candidate with { PeakLevel = 0 }));
    }

    private void DispatchToUi(Action callback)
    {
        if (_uiDispatcher is null || _uiDispatcher.HasThreadAccess)
        {
            callback();
            return;
        }

        _uiDispatcher.TryEnqueue(callback);
    }

    private string Text(string key, string fallback, params object?[] arguments)
    {
        var value = _localization?.Get(key, arguments);
        return value is not null && value != key
            ? value
            : string.Format(fallback, arguments);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _service.SnapshotChanged -= OnSnapshotChanged;
        _settings.Changed -= OnSettingsChanged;
        if (_mixer is not null)
        {
            if (_mixerActive)
            {
                _mixerActive = false;
                _mixer.SetMeteringEnabled(false);
            }

            _mixer.MixerChanged -= OnMixerChanged;
        }
        if (_localization is not null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }
    }
}
