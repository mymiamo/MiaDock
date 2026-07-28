using MiaDock.Core.Localization;
using MiaDock.Core.Modules;
using MiaDock.Core.Updates;

namespace MiaDock.App.Modules;

public sealed class StoreUpdateModule(
    IStoreUpdateService storeUpdateService,
    ILocalizationService localization) : IIslandModule
{
    public const string ModuleId = "store-update";
    public const string OpenStoreCommandId = "open-store";
    private static readonly TimeSpan NotificationDuration = TimeSpan.FromSeconds(8);

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleId,
        "Yeni sürüm mevcut",
        625,
        "GenericCompactModuleView",
        "GenericExpandedModuleView",
        new HashSet<ModuleEventKind> { ModuleEventKind.UpdateAvailable },
        NotificationDuration,
        [new ModuleCommandDescriptor(OpenStoreCommandId, "Microsoft Store'da aç", "\uE7BF")],
        notificationViewKey: "StoreUpdateNotificationView",
        persistentPriority: 0,
        isPersistent: false,
        iconGlyph: "\uE895",
        displayNameKey: "Update.Available");

    public ModuleLifecycleState LifecycleState { get; private set; }

    public bool IsEnabled { get; set; } = true;

    public ModulePresentation? CurrentPresentation => null;

    public event EventHandler<ModulePresentation?>? PresentationChanged;

    public event EventHandler<ModuleEvent>? EventOccurred;

    public bool CanExecuteCommand(string commandId) =>
        LifecycleState == ModuleLifecycleState.Active &&
        string.Equals(commandId, OpenStoreCommandId, StringComparison.Ordinal);

    public async ValueTask<bool> ExecuteCommandAsync(
        string commandId,
        CancellationToken cancellationToken = default) =>
        CanExecuteCommand(commandId) &&
        await storeUpdateService.OpenStorePageAsync(cancellationToken);

    public ValueTask ActivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LifecycleState = ModuleLifecycleState.Active;
        PresentationChanged?.Invoke(this, null);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LifecycleState = ModuleLifecycleState.Inactive;
        PresentationChanged?.Invoke(this, null);
        return ValueTask.CompletedTask;
    }

    public void PublishAvailable(StoreUpdateSnapshot update)
    {
        if (!IsEnabled ||
            LifecycleState != ModuleLifecycleState.Active ||
            update.Status != StoreUpdateStatus.UpdateAvailable ||
            update.AvailableVersion is null)
        {
            return;
        }

        var presentation = new ModulePresentation(
            ModuleId,
            localization.Get("Update.Available"),
            localization.Get(
                "Update.VersionPair",
                update.CurrentVersion,
                update.AvailableVersion),
            "\uE895",
            ModuleIndicatorKind.StatusDot,
            valueText: localization.Get("Update.OpenStore"),
            presentationKind: ModulePresentationKind.Alert,
            commands:
            [
                new ModuleCommandState(
                    OpenStoreCommandId,
                    localization.Get("Update.OpenStore"),
                    "\uE7BF",
                    true)
            ],
            isPersistentOverride: false);
        EventOccurred?.Invoke(this, new ModuleEvent(
            ModuleId,
            ModuleEventKind.UpdateAvailable,
            presentation,
            NotificationDuration,
            DateTimeOffset.UtcNow,
            ModuleEventPriority.Normal,
            $"store-update:{update.AvailableVersion}",
            isFullscreenEligible: false));
    }
}
