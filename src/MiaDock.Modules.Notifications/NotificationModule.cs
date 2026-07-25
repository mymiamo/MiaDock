using MiaDock.Core.Modules;
using MiaDock.Modules.Notifications.Models;
using MiaDock.Modules.Notifications.Services;
using MiaDock.Modules.Notifications.Settings;

namespace MiaDock.Modules.Notifications;

public sealed class NotificationModule : IIslandModule, IDisposable
{
    public const string ModuleId = "notifications";
    private readonly ISystemNotificationService _service;
    private readonly INotificationModuleSettings _settings;
    private ModulePresentation? _current;
    private bool _isEnabled;

    public NotificationModule(ISystemNotificationService service, INotificationModuleSettings settings)
    {
        _service = service;
        _settings = settings;
        _isEnabled = settings.Current.IsEnabled;
        _service.NotificationReceived += OnNotificationReceived;
    }

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleId,
        "Bildirimler",
        650,
        "NotificationCompactView",
        "NotificationExpandedView",
        new HashSet<ModuleEventKind> { ModuleEventKind.Notification },
        TimeSpan.FromSeconds(5),
        notificationViewKey: "NotificationModuleNotificationView",
        persistentPriority: 0,
        isPersistent: false,
        iconGlyph: "\uEA8F");

    public ModuleLifecycleState LifecycleState { get; private set; }
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; PresentationChanged?.Invoke(this, CurrentPresentation); } }
    public ModulePresentation? CurrentPresentation => LifecycleState == ModuleLifecycleState.Active ? _current : null;
    public event EventHandler<ModulePresentation?>? PresentationChanged;
    public event EventHandler<ModuleEvent>? EventOccurred;
    public bool CanExecuteCommand(string commandId) => false;
    public ValueTask<bool> ExecuteCommandAsync(string commandId, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);

    public ValueTask ActivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LifecycleState = ModuleLifecycleState.Active;
        PresentationChanged?.Invoke(this, CurrentPresentation);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LifecycleState = ModuleLifecycleState.Inactive;
        _current = null;
        PresentationChanged?.Invoke(this, null);
        return ValueTask.CompletedTask;
    }

    private void OnNotificationReceived(object? sender, SystemNotificationSnapshot snapshot)
    {
        if (LifecycleState != ModuleLifecycleState.Active || !_settings.Current.IsApplicationAllowed(snapshot.SourceId))
        {
            return;
        }

        var showBody = _settings.Current.CanShowBody(snapshot.SourceId);
        var presentation = new ModulePresentation(
            ModuleId,
            snapshot.SourceDisplayName,
            snapshot.Title,
            "\uEA8F",
            ModuleIndicatorKind.StatusDot,
            valueText: showBody ? snapshot.Body : null,
            isSensitive: true,
            presentationKind: ModulePresentationKind.Alert);
        _current = presentation;
        PresentationChanged?.Invoke(this, presentation);
        EventOccurred?.Invoke(this, new ModuleEvent(
            ModuleId,
            ModuleEventKind.Notification,
            presentation,
            _settings.Current.EventDuration,
            DateTimeOffset.UtcNow,
            ModuleEventPriority.High,
            $"notification:{snapshot.SourceId}:{snapshot.Id}",
            isFullscreenEligible: _settings.Current.ShowInFullscreen));
    }

    public void Dispose() => _service.NotificationReceived -= OnNotificationReceived;
}
