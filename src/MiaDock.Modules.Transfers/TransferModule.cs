using MiaDock.Core.Modules;
using MiaDock.Core.Localization;
using MiaDock.Modules.Transfers.Models;
using MiaDock.Modules.Transfers.Services;
using MiaDock.Modules.Transfers.Settings;
using MiaDock.Modules.Transfers.ViewModels;

namespace MiaDock.Modules.Transfers;

public sealed class TransferModule : IIslandModule, IDisposable
{
    public const string ModuleId = "transfers";
    private readonly ITransferStateService _service;
    private readonly TransferModuleViewModel _viewModel;
    private readonly ITransferModuleSettings _settings;
    private readonly ILocalizationService? _localization;
    private bool _isEnabled;

    public TransferModule(
        ITransferStateService service,
        TransferModuleViewModel viewModel,
        ITransferModuleSettings settings,
        ILocalizationService? localization = null)
    {
        _service = service;
        _viewModel = viewModel;
        _settings = settings;
        _localization = localization;
        _isEnabled = settings.Current.IsEnabled;
        _service.SnapshotChanged += OnSnapshotChanged;
        _service.TransfersChanged += OnTransfersChanged;
        _settings.Changed += OnSettingsChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged += OnLanguageChanged;
        }
    }

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleId,
        "Aktarımlar",
        420,
        "TransferCompactView",
        "TransferExpandedView",
        new HashSet<ModuleEventKind>
        {
            ModuleEventKind.ProgressChanged,
            ModuleEventKind.Completed,
            ModuleEventKind.Warning
        },
        TimeSpan.FromSeconds(5),
        notificationViewKey: "TransferNotificationView",
        persistentPriority: 400,
        isPersistent: false,
        iconGlyph: "\uE896",
        minimumExpandedHeight: 350);

    public ModuleLifecycleState LifecycleState { get; private set; }
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            _isEnabled = value;
            PresentationChanged?.Invoke(this, CurrentPresentation);
        }
    }

    public ModulePresentation? CurrentPresentation =>
        LifecycleState == ModuleLifecycleState.Active && _viewModel.Current is { } current
            ? CreatePresentation(current, persistent: true)
            : null;

    public event EventHandler<ModulePresentation?>? PresentationChanged;
    public event EventHandler<ModuleEvent>? EventOccurred;

    public bool CanExecuteCommand(string commandId) => false;
    public ValueTask<bool> ExecuteCommandAsync(string commandId, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(false);

    public async ValueTask ActivateAsync(CancellationToken cancellationToken = default)
    {
        await _service.StartAsync(cancellationToken);
        LifecycleState = ModuleLifecycleState.Active;
        PresentationChanged?.Invoke(this, CurrentPresentation);
    }

    public async ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
    {
        LifecycleState = ModuleLifecycleState.Inactive;
        await _service.StopAsync(cancellationToken);
        PresentationChanged?.Invoke(this, null);
    }

    public void Dispose()
    {
        _service.SnapshotChanged -= OnSnapshotChanged;
        _service.TransfersChanged -= OnTransfersChanged;
        _settings.Changed -= OnSettingsChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }
    }

    private void OnSnapshotChanged(object? sender, TransferSnapshot snapshot)
    {
        if (LifecycleState != ModuleLifecycleState.Active || !snapshot.IsTerminal) return;
        var kind = snapshot.Status == TransferStatus.Completed
            ? ModuleEventKind.Completed
            : ModuleEventKind.Warning;
        EventOccurred?.Invoke(this, new ModuleEvent(
            ModuleId,
            kind,
            CreatePresentation(snapshot, persistent: false),
            _settings.Current.EventDuration,
            DateTimeOffset.UtcNow,
            ModuleEventPriority.Elevated,
            $"transfer:{snapshot.ProviderId}:{snapshot.TransferId}",
            isFullscreenEligible: _settings.Current.ShowInFullscreen));
    }

    private void OnTransfersChanged(object? sender, IReadOnlyList<TransferSnapshot> transfers) =>
        PresentationChanged?.Invoke(this, CurrentPresentation);

    private void OnSettingsChanged(object? sender, TransferModuleOptions options) =>
        PresentationChanged?.Invoke(this, CurrentPresentation);

    private ModulePresentation CreatePresentation(TransferSnapshot snapshot, bool persistent) => new(
        ModuleId,
        snapshot.SafeDisplayName,
        _viewModel.StatusToText(snapshot.Status),
        "\uE896",
        snapshot.Status is TransferStatus.Failed or TransferStatus.Disconnected
            ? ModuleIndicatorKind.StatusDot
            : ModuleIndicatorKind.Value,
        valueText: snapshot.TotalBytes > 0
            ? $"{TransferModuleViewModel.FormatBytes(snapshot.TransferredBytes)} / {TransferModuleViewModel.FormatBytes(snapshot.TotalBytes)}"
            : TransferModuleViewModel.FormatBytes(snapshot.TransferredBytes),
        progress: snapshot.Progress,
        isSensitive: true,
        presentationKind: snapshot.IsTerminal
            ? (snapshot.Status == TransferStatus.Completed ? ModulePresentationKind.Status : ModulePresentationKind.Alert)
            : ModulePresentationKind.Progress,
        isPersistentOverride: persistent,
        persistentPriorityOverride: persistent ? 400 : null);

    private void OnLanguageChanged(object? sender, EventArgs args) =>
        PresentationChanged?.Invoke(this, CurrentPresentation);
}
