using MiaDock.Core.Localization;
using MiaDock.Core.Modules;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.Services;
using MiaDock.Modules.SystemStatus.Settings;
using MiaDock.Modules.SystemStatus.ViewModels;

namespace MiaDock.Modules.SystemStatus;

public sealed class VolumeModule : IIslandModule, IDisposable
{
    public const string ModuleId = "volume";

    private readonly ISystemActivityService _service;
    private readonly VolumeModuleViewModel _viewModel;
    private readonly IVolumeModuleSettings _settings;
    private readonly ILocalizationService? _localization;
    private SystemActivitySnapshot? _previousSnapshot;
    private bool _isEnabled = true;

    public VolumeModule(
        ISystemActivityService service,
        VolumeModuleViewModel viewModel,
        IVolumeModuleSettings settings,
        ILocalizationService? localization = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _localization = localization;
        _service.SnapshotChanged += OnSnapshotChanged;
        _settings.Changed += OnSettingsChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged += OnLanguageChanged;
        }
    }

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleId,
        "Ses",
        210,
        "VolumeCompactView",
        "VolumeExpandedView",
        new HashSet<ModuleEventKind>
        {
            ModuleEventKind.ValueChanged,
            ModuleEventKind.StatusChanged
        },
        TimeSpan.FromSeconds(2.5),
        [
            new ModuleCommandDescriptor("master-mute", "Ana sesi aç veya kapat", "\uE74F"),
            new ModuleCommandDescriptor("open-sound-settings", "Windows ses ayarlarını aç", "\uE713")
        ],
        "VolumeNotificationView",
        persistentPriority: 0,
        isPersistent: false,
        iconGlyph: "\uE995",
        minimumExpandedHeight: 400);

    public ModuleLifecycleState LifecycleState { get; private set; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            PresentationChanged?.Invoke(this, CurrentPresentation);
        }
    }

    public ModulePresentation? CurrentPresentation => LifecycleState == ModuleLifecycleState.Active
        ? CreatePresentation(_service.Current)
        : null;

    public event EventHandler<ModulePresentation?>? PresentationChanged;

    public event EventHandler<ModuleEvent>? EventOccurred;

    public bool CanExecuteCommand(string commandId) => commandId switch
    {
        "master-mute" => _service.Current.IsMasterVolumeAvailable,
        "open-sound-settings" => true,
        _ => false
    };

    public async ValueTask<bool> ExecuteCommandAsync(
        string commandId,
        CancellationToken cancellationToken = default)
    {
        if (!CanExecuteCommand(commandId))
        {
            return false;
        }

        return commandId switch
        {
            "master-mute" => await _service.ToggleMasterMuteAsync(cancellationToken),
            "open-sound-settings" =>
                await _viewModel.OpenSoundSettingsAsync(cancellationToken),
            _ => false
        };
    }

    public async ValueTask ActivateAsync(CancellationToken cancellationToken = default)
    {
        await _service.StartAsync(cancellationToken);
        LifecycleState = ModuleLifecycleState.Active;
        _previousSnapshot = _service.Current;
        PresentationChanged?.Invoke(this, CurrentPresentation);
    }

    public ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LifecycleState = ModuleLifecycleState.Inactive;
        PresentationChanged?.Invoke(this, null);
        return ValueTask.CompletedTask;
    }

    private void OnSnapshotChanged(object? sender, SystemActivitySnapshot snapshot)
    {
        var previous = _previousSnapshot;
        _previousSnapshot = snapshot;
        if (LifecycleState != ModuleLifecycleState.Active)
        {
            return;
        }

        PresentationChanged?.Invoke(this, CurrentPresentation);
        if (previous is null)
        {
            return;
        }

        var moduleEvent = CreateEvent(previous, snapshot);
        if (moduleEvent is not null)
        {
            EventOccurred?.Invoke(this, moduleEvent);
        }
    }

    private ModuleEvent? CreateEvent(
        SystemActivitySnapshot previous,
        SystemActivitySnapshot current)
    {
        var now = DateTimeOffset.UtcNow;
        if (current.IsMasterVolumeAvailable &&
            (previous.IsMasterMuted != current.IsMasterMuted ||
             Math.Abs(previous.MasterVolume - current.MasterVolume) >= 0.001))
        {
            return new ModuleEvent(
                ModuleId,
                ModuleEventKind.ValueChanged,
                CreatePresentation(current),
                _settings.Current.EventDuration,
                now,
                ModuleEventPriority.Low,
                "volume:master",
                isFullscreenEligible: _settings.Current.ShowInFullscreen);
        }

        if (!string.Equals(
                previous.DefaultOutputDeviceId,
                current.DefaultOutputDeviceId,
                StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(current.DefaultOutputDeviceId))
        {
            return new ModuleEvent(
                ModuleId,
                ModuleEventKind.StatusChanged,
                CreatePresentation(current),
                _settings.Current.EventDuration,
                now,
                ModuleEventPriority.Normal,
                "volume:output-device",
                isFullscreenEligible: _settings.Current.ShowInFullscreen);
        }

        if (current.ApplicationVolumeAvailability == ApplicationVolumeAvailability.Available &&
            (previous.IsApplicationMuted != current.IsApplicationMuted ||
             Math.Abs(previous.ApplicationVolume - current.ApplicationVolume) >= 0.001))
        {
            return new ModuleEvent(
                ModuleId,
                ModuleEventKind.ValueChanged,
                new ModulePresentation(
                    ModuleId,
                    current.IsApplicationMuted
                        ? Text("System.Audio.Application.Muted", "Uygulama sesi kapalı")
                        : Text("System.Audio.Application", "Uygulama sesi"),
                    Text("System.Audio.SelectedMedia", "Seçili medya uygulaması"),
                    "\uE74F",
                    ModuleIndicatorKind.None,
                    valueText: $"{current.ApplicationVolumePercent}%",
                    progress: current.ApplicationVolume,
                    presentationKind: ModulePresentationKind.Status),
                _settings.Current.EventDuration,
                now,
                ModuleEventPriority.Low,
                "volume:application",
                isFullscreenEligible: _settings.Current.ShowInFullscreen);
        }

        return null;
    }

    private ModulePresentation CreatePresentation(SystemActivitySnapshot snapshot)
    {
        var secondary = _settings.Current.ShowOutputDeviceName &&
                        !string.IsNullOrWhiteSpace(snapshot.DefaultOutputDeviceName)
            ? snapshot.DefaultOutputDeviceName
            : Text("Volume.WindowsAudio", "Windows ana sesi");
        return new ModulePresentation(
            ModuleId,
            snapshot.IsMasterMuted
                ? Text("Volume.Muted", "Ses kapalı")
                : Text("Volume.Master", "Ana ses"),
            secondary,
            _viewModel.VolumeGlyph,
            ModuleIndicatorKind.None,
            valueText: snapshot.IsMasterVolumeAvailable
                ? $"{snapshot.MasterVolumePercent}%"
                : "—",
            progress: snapshot.IsMasterVolumeAvailable ? snapshot.MasterVolume : null,
            presentationKind: ModulePresentationKind.Status,
            commands:
            [
                new ModuleCommandState(
                    "master-mute",
                    Text("Volume.ToggleMute", "Sesi aç veya kapat"),
                    "\uE74F",
                    snapshot.IsMasterVolumeAvailable),
                new ModuleCommandState(
                    "open-sound-settings",
                    Text("Volume.OpenSettings", "Windows ses ayarlarını aç"),
                    "\uE713",
                    true)
            ]);
    }

    private void OnSettingsChanged(object? sender, VolumeModuleOptions options) =>
        PresentationChanged?.Invoke(this, CurrentPresentation);

    private void OnLanguageChanged(object? sender, EventArgs args) =>
        PresentationChanged?.Invoke(this, CurrentPresentation);

    private string Text(string key, string fallback) =>
        _localization?.Get(key) is { } value && value != key ? value : fallback;

    public void Dispose()
    {
        _service.SnapshotChanged -= OnSnapshotChanged;
        _settings.Changed -= OnSettingsChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }
    }
}
