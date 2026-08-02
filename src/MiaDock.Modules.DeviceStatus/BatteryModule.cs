using MiaDock.Core.Modules;
using MiaDock.Core.Localization;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using MiaDock.Modules.DeviceStatus.Settings;
using MiaDock.Modules.DeviceStatus.ViewModels;

namespace MiaDock.Modules.DeviceStatus;

public sealed class BatteryModule : IIslandModule, IDisposable
{
    public const string ModuleId = "battery";
    private readonly IPowerStatusService _service;
    private readonly BatteryModuleViewModel _viewModel;
    private readonly IBatteryModuleSettings _settings;
    private readonly ILocalizationService? _localization;
    private readonly HashSet<int> _firedThresholds = [];
    private BatteryStatusSnapshot? _previous;
    private bool _isEnabled = true;

    public BatteryModule(
        IPowerStatusService service,
        BatteryModuleViewModel viewModel,
        IBatteryModuleSettings settings,
        ILocalizationService? localization = null)
    {
        _service = service;
        _viewModel = viewModel;
        _settings = settings;
        _localization = localization;
        _service.SnapshotChanged += OnSnapshotChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged += OnLanguageChanged;
        }
    }

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleId, "Pil", 400, "BatteryCompactView", "BatteryExpandedView",
        new HashSet<ModuleEventKind> { ModuleEventKind.Warning, ModuleEventKind.Critical, ModuleEventKind.StatusChanged },
        TimeSpan.FromSeconds(5), notificationViewKey: "BatteryNotificationView",
        persistentPriority: 0, isPersistent: false, iconGlyph: "\uE850",
        minimumExpandedHeight: 300);

    public ModuleLifecycleState LifecycleState { get; private set; }
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; PresentationChanged?.Invoke(this, CurrentPresentation); } }
    public ModulePresentation? CurrentPresentation => LifecycleState == ModuleLifecycleState.Active && _service.Current.IsBatteryPresent
        ? CreatePresentation(_service.Current)
        : null;
    public event EventHandler<ModulePresentation?>? PresentationChanged;
    public event EventHandler<ModuleEvent>? EventOccurred;
    public bool CanExecuteCommand(string commandId) => false;
    public ValueTask<bool> ExecuteCommandAsync(string commandId, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);

    public async ValueTask ActivateAsync(CancellationToken cancellationToken = default)
    {
        await _service.StartAsync(cancellationToken);
        LifecycleState = ModuleLifecycleState.Active;
        _previous = _service.Current;
        PresentationChanged?.Invoke(this, CurrentPresentation);
    }

    public async ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
    {
        LifecycleState = ModuleLifecycleState.Inactive;
        await _service.StopAsync(cancellationToken);
        PresentationChanged?.Invoke(this, null);
    }

    private void OnSnapshotChanged(object? sender, BatteryStatusSnapshot current)
    {
        var previous = _previous;
        _previous = current;
        if (LifecycleState != ModuleLifecycleState.Active) return;
        PresentationChanged?.Invoke(this, CurrentPresentation);
        if (previous is null || !current.IsBatteryPresent) return;
        if (current.IsCharging)
        {
            _firedThresholds.Clear();
            return;
        }

        var options = _settings.Current;
        var crossed = new[] { options.EmergencyThresholdPercent, options.CriticalThresholdPercent, options.LowThresholdPercent }
            .FirstOrDefault(threshold => previous.ChargePercent > threshold && current.ChargePercent <= threshold && !_firedThresholds.Contains(threshold));
        if (crossed <= 0) return;
        _firedThresholds.Add(crossed);
        var priority = crossed <= options.EmergencyThresholdPercent
            ? ModuleEventPriority.Critical
            : crossed <= options.CriticalThresholdPercent ? ModuleEventPriority.High : ModuleEventPriority.Normal;
        EventOccurred?.Invoke(this, new ModuleEvent(
            ModuleId,
            priority == ModuleEventPriority.Critical ? ModuleEventKind.Critical : ModuleEventKind.Warning,
            CreatePresentation(current),
            options.EventDuration,
            DateTimeOffset.UtcNow,
            priority,
            "battery:low",
            isFullscreenEligible: options.ShowInFullscreen));
    }

    private ModulePresentation CreatePresentation(BatteryStatusSnapshot snapshot) => new(
        ModuleId,
        snapshot.IsCharging
            ? Text("Battery.Charging.Title", "Pil şarj oluyor")
            : Text("Battery.Percent", "Pil %{0}", snapshot.ChargePercent),
        snapshot.IsEnergySaverOn ? Text("Battery.EnergySaver", "Enerji tasarrufu açık") : _viewModel.PowerText,
        _viewModel.BatteryGlyph,
        ModuleIndicatorKind.Value,
        valueText: $"%{snapshot.ChargePercent}",
        progress: snapshot.ChargePercent / 100d,
        presentationKind: snapshot.ChargePercent <= _settings.Current.CriticalThresholdPercent ? ModulePresentationKind.Alert : ModulePresentationKind.Status);

    private void OnLanguageChanged(object? sender, EventArgs args) =>
        PresentationChanged?.Invoke(this, CurrentPresentation);

    private string Text(string key, string fallback, params object?[] arguments)
    {
        var value = _localization?.Get(key, arguments);
        return value is not null && value != key ? value : string.Format(fallback, arguments);
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
