using MiaDock.Core.Modules;
using MiaDock.Core.Localization;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.Services;
using MiaDock.Modules.SystemStatus.ViewModels;

namespace MiaDock.Modules.SystemStatus;

public sealed class SystemActivityModule : IIslandModule, IDisposable
{
    public const string ModuleId = "system-activity";

    private readonly ISystemActivityService _service;
    private readonly SystemActivityViewModel _viewModel;
    private readonly ILocalizationService? _localization;
    private SystemActivitySnapshot? _previousSnapshot;
    private bool _isEnabled = true;

    public SystemActivityModule(
        ISystemActivityService service,
        SystemActivityViewModel viewModel,
        ILocalizationService? localization = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _localization = localization;
        _service.SnapshotChanged += OnSnapshotChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged += OnLanguageChanged;
        }
    }

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleId,
        "Sistem",
        200,
        "SystemActivityCompactView",
        "SystemActivityExpandedView",
        new HashSet<ModuleEventKind>
        {
            ModuleEventKind.ValueChanged,
            ModuleEventKind.StatusChanged
        },
        TimeSpan.FromSeconds(3),
        [
            new ModuleCommandDescriptor("master-volume-down", "Ana sesi azalt", "\uE993"),
            new ModuleCommandDescriptor("master-mute", "Ana sesi aç veya kapat", "\uE74F"),
            new ModuleCommandDescriptor("master-volume-up", "Ana sesi artır", "\uE995"),
            new ModuleCommandDescriptor("app-mute", "Uygulama sesini aç veya kapat", "\uE74F")
        ],
        "SystemActivityNotificationView",
        persistentPriority: 0,
        isPersistent: false,
        iconGlyph: "\uE74F");

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
        ? CreatePresentation(_viewModel.Snapshot)
        : null;

    public event EventHandler<ModulePresentation?>? PresentationChanged;

    public event EventHandler<ModuleEvent>? EventOccurred;

    public bool CanExecuteCommand(string commandId) => commandId switch
    {
        "master-volume-down" or "master-mute" or "master-volume-up" =>
            _service.Current.IsMasterVolumeAvailable,
        "app-mute" => _service.Current.ApplicationVolumeAvailability == ApplicationVolumeAvailability.Available,
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
            "master-volume-down" => await _service.SetMasterVolumeAsync(
                Math.Max(0, _service.Current.MasterVolume - 0.05), cancellationToken),
            "master-mute" => await _service.ToggleMasterMuteAsync(cancellationToken),
            "master-volume-up" => await _service.SetMasterVolumeAsync(
                Math.Min(1, _service.Current.MasterVolume + 0.05), cancellationToken),
            "app-mute" => await _service.ToggleApplicationMuteAsync(cancellationToken),
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

    private ModuleEvent? CreateEvent(SystemActivitySnapshot previous, SystemActivitySnapshot current)
    {
        var now = DateTimeOffset.UtcNow;
        if (previous.CallActivity != current.CallActivity)
        {
            return CreateStatusEvent(
                current.CallActivity == CallActivityState.Possible
                    ? Text("Dock.CallPossible", "Olası arama etkinliği")
                    : Text("System.Call.Ended", "Arama etkinliği sona erdi"),
                current.CallActivity == CallActivityState.Possible
                    ? Text("System.Call.Detail", "Mikrofon ve iletişim sesi etkin")
                    : string.Empty,
                "\uE717",
                ModuleEventPriority.High,
                "system:call",
                now);
        }

        if (previous.MicrophoneUsage != current.MicrophoneUsage)
        {
            return CreateStatusEvent(
                current.MicrophoneUsage == MicrophoneUsageState.Active
                    ? Text("System.Microphone.Active", "Mikrofon etkin")
                    : Text("System.Microphone.Idle", "Mikrofon boşta"),
                Text("System.AudioSession.State", "Windows ses oturumu durumu"),
                "\uE720",
                ModuleEventPriority.High,
                "system:microphone",
                now);
        }

        if (previous.IsMasterMuted != current.IsMasterMuted ||
            Math.Abs(previous.MasterVolume - current.MasterVolume) >= 0.001)
        {
            var presentation = CreatePresentation(current);
            return new ModuleEvent(
                ModuleId,
                ModuleEventKind.ValueChanged,
                presentation,
                TimeSpan.FromSeconds(2),
                now,
                ModuleEventPriority.Low,
                "system:master-volume");
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
                    valueText: $"%{current.ApplicationVolumePercent}",
                    progress: current.ApplicationVolume,
                    presentationKind: ModulePresentationKind.Status),
                TimeSpan.FromSeconds(2),
                now,
                ModuleEventPriority.Low,
                "system:application-volume");
        }

        if (previous.CameraDeviceAvailability != current.CameraDeviceAvailability ||
            previous.CameraAccess != current.CameraAccess)
        {
            return CreateStatusEvent(
                current.CameraDeviceAvailability == CameraDeviceAvailability.Available
                    ? Text("System.Camera.Changed", "Kamera kullanılabilirliği değişti")
                    : _viewModel.CameraText,
                _viewModel.CameraText,
                "\uE714",
                ModuleEventPriority.Normal,
                "system:camera",
                now);
        }

        return null;
    }

    private ModuleEvent CreateStatusEvent(
        string title,
        string secondary,
        string glyph,
        ModuleEventPriority priority,
        string key,
        DateTimeOffset now) => new(
        ModuleId,
        ModuleEventKind.StatusChanged,
        new ModulePresentation(
            ModuleId,
            title,
            secondary,
            glyph,
            ModuleIndicatorKind.None,
            presentationKind: ModulePresentationKind.Status),
        Descriptor.DefaultDisplayDuration,
        now,
        priority,
        key);

    private ModulePresentation CreatePresentation(SystemActivitySnapshot snapshot)
    {
        var hasOngoingActivity =
            snapshot.CallActivity == CallActivityState.Possible ||
            snapshot.MicrophoneUsage == MicrophoneUsageState.Active;
        return new ModulePresentation(
            ModuleId,
            snapshot.IsMasterMuted
                ? Text("System.Audio.Muted", "Ses kapalı")
                : Text("System.Audio.Master", "Sistem sesi"),
            snapshot.CallActivity == CallActivityState.Possible
                ? Text("Dock.CallPossible", "Olası arama etkinliği")
                : _viewModel.MicrophoneText,
            _viewModel.MasterVolumeGlyph,
            snapshot.MicrophoneUsage == MicrophoneUsageState.Active
                ? ModuleIndicatorKind.ActivityBars
                : ModuleIndicatorKind.None,
            valueText: snapshot.IsMasterVolumeAvailable ? $"%{snapshot.MasterVolumePercent}" : "—",
            progress: snapshot.IsMasterVolumeAvailable ? snapshot.MasterVolume : null,
            presentationKind: ModulePresentationKind.Status,
            commands:
            [
                new ModuleCommandState(
                    "master-volume-down",
                    Text("System.Audio.Master.Down", "Ana sesi azalt"),
                    "\uE993",
                    CanExecuteCommand("master-volume-down")),
                new ModuleCommandState(
                    "master-mute",
                    Text("System.Audio.Master.Toggle", "Ana sesi aç veya kapat"),
                    "\uE74F",
                    CanExecuteCommand("master-mute")),
                new ModuleCommandState(
                    "master-volume-up",
                    Text("System.Audio.Master.Up", "Ana sesi artır"),
                    "\uE995",
                    CanExecuteCommand("master-volume-up")),
                new ModuleCommandState(
                    "app-mute",
                    Text("System.Audio.Application.Toggle", "Uygulama sesini aç veya kapat"),
                    "\uE74F",
                    CanExecuteCommand("app-mute"))
            ],
            isPersistentOverride: hasOngoingActivity,
            persistentPriorityOverride: hasOngoingActivity ? 450 : null);
    }

    private void OnLanguageChanged(object? sender, EventArgs args) =>
        PresentationChanged?.Invoke(this, CurrentPresentation);

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
