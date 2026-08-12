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
            ModuleEventKind.StatusChanged
        },
        TimeSpan.FromSeconds(3),
        [],
        "SystemActivityNotificationView",
        persistentPriority: 0,
        isPersistent: false,
        iconGlyph: "\uE717",
        minimumExpandedHeight: 280);

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

    public bool CanExecuteCommand(string commandId) => false;

    public async ValueTask<bool> ExecuteCommandAsync(
        string commandId,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return false;
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
        if (previous.CallActivity == current.CallActivity)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
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
        var hasOngoingActivity = snapshot.CallActivity == CallActivityState.Possible;
        return new ModulePresentation(
            ModuleId,
            _viewModel.ActivityTitle,
            _viewModel.ActivityDetail,
            _viewModel.ActivityGlyph,
            hasOngoingActivity ? ModuleIndicatorKind.StatusDot : ModuleIndicatorKind.None,
            valueText: string.Empty,
            presentationKind: ModulePresentationKind.Status,
            commands: [],
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
