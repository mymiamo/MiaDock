using System.Security.Cryptography;
using System.Text;
using MiaDock.Core.Modules;
using MiaDock.Core.Localization;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using MiaDock.Modules.DeviceStatus.Settings;
using MiaDock.Modules.DeviceStatus.ViewModels;

namespace MiaDock.Modules.DeviceStatus;

public sealed class DeviceHubModule : IIslandModule, IAsyncDisposable
{
    public const string ModuleId = "device-hub";
    private readonly IDeviceHubService _service;
    private readonly DeviceHubViewModel _viewModel;
    private readonly ILocalizationService? _localization;
    private readonly IDeviceHubSettings _settings;
    private readonly Dictionary<string, NotificationAction> _notificationActions = new(StringComparer.Ordinal);
    private bool _isEnabled = true;

    public DeviceHubModule(
        IDeviceHubService service,
        DeviceHubViewModel viewModel,
        IDeviceHubSettings settings,
        ILocalizationService? localization = null)
    {
        _service = service;
        _viewModel = viewModel;
        _settings = settings;
        _localization = localization;
        _service.StateChanged += OnStateChanged;
        _service.DeviceChanged += OnDeviceChanged;
        if (_localization is not null) _localization.LanguageChanged += OnLanguageChanged;
    }

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleId, "Device Hub", 275, "DeviceHubCompactView", "DeviceHubExpandedView",
        new HashSet<ModuleEventKind> { ModuleEventKind.StatusChanged }, TimeSpan.FromSeconds(4),
        notificationViewKey: "DeviceHubNotificationView",
        persistentPriority: 0, isPersistent: false, iconGlyph: "\uE7F4", minimumExpandedHeight: 390,
        displayNameKey: "DeviceHub.Title");

    public ModuleLifecycleState LifecycleState { get; private set; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set { if (_isEnabled != value) { _isEnabled = value; PresentationChanged?.Invoke(this, CurrentPresentation); } }
    }

    public ModulePresentation? CurrentPresentation => LifecycleState == ModuleLifecycleState.Active
        ? new ModulePresentation(ModuleId, Text("DeviceHub.Title", "Device Hub"), _viewModel.StatusText, "\uE7F4",
            _service.Current.BluetoothDevices.Any(device => device.ConnectionState == DeviceHubConnectionState.Connected)
                ? ModuleIndicatorKind.StatusDot : ModuleIndicatorKind.None,
            valueText: ConnectedCount().ToString(), presentationKind: ModulePresentationKind.Status)
        : null;

    public event EventHandler<ModulePresentation?>? PresentationChanged;
    public event EventHandler<ModuleEvent>? EventOccurred;
    public bool CanExecuteCommand(string commandId) => _notificationActions.ContainsKey(commandId);

    public async ValueTask<bool> ExecuteCommandAsync(string commandId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_notificationActions.TryGetValue(commandId, out var action))
        {
            return false;
        }

        switch (action.Kind)
        {
            case NotificationActionKind.OpenStorage:
                await _viewModel.OpenStorageDeviceAsync(action.Device);
                break;
            case NotificationActionKind.ManageBluetooth:
                await _viewModel.OpenBluetoothSettingsPageAsync();
                break;
            case NotificationActionKind.ManageSound:
                await _viewModel.OpenSoundSettingsPageAsync();
                break;
            default:
                return false;
        }

        return true;
    }

    public async ValueTask ActivateAsync(CancellationToken cancellationToken = default)
    {
        LifecycleState = ModuleLifecycleState.Active;
        await _service.StartAsync(cancellationToken);
        PresentationChanged?.Invoke(this, CurrentPresentation);
    }

    public async ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
    {
        LifecycleState = ModuleLifecycleState.Inactive;
        await _service.StopAsync(cancellationToken);
        PresentationChanged?.Invoke(this, null);
    }

    private void OnStateChanged(object? sender, DeviceHubState state)
    {
        if (LifecycleState == ModuleLifecycleState.Active) PresentationChanged?.Invoke(this, CurrentPresentation);
    }

    private void OnDeviceChanged(object? sender, DeviceHubChange change)
    {
        if (LifecycleState != ModuleLifecycleState.Active || !IsEnabled) return;
        var title = change.Kind switch
        {
            DeviceHubChangeKind.Connected => Text("DeviceHub.Connected", "Cihaz bağlandı"),
            DeviceHubChangeKind.Disconnected => Text("DeviceHub.Disconnected", "Cihaz ayrıldı"),
            DeviceHubChangeKind.BatteryLow => Text("DeviceHub.BatteryLow", "Pil düşük"),
            DeviceHubChangeKind.DefaultAudioOutputChanged => Text("DeviceHub.AudioOutputChanged", "Ses çıkışı değişti"),
            DeviceHubChangeKind.SafeToRemove => Text("DeviceHub.SafeToRemove", "Çıkarmak güvenli"),
            _ => Text("DeviceHub.Title", "Device Hub")
        };
        var detail = change.Kind == DeviceHubChangeKind.BatteryLow && change.Device.BatteryPercentage is { } battery
            ? $"{change.Device.DisplayName} · {battery}%"
            : change.Device.DisplayName;
        var command = CreateNotificationCommand(change);
        var presentation = new ModulePresentation(ModuleId, title, detail, Glyph(change.Device.Category),
            ModuleIndicatorKind.StatusDot,
            isSensitive: true,
            presentationKind: ModulePresentationKind.Status,
            commands: command is null ? null : [command]);
        EventOccurred?.Invoke(this, new ModuleEvent(ModuleId, ModuleEventKind.StatusChanged, presentation,
            _settings.Current.EventDuration, DateTimeOffset.UtcNow, ModuleEventPriority.Normal,
            $"device-hub:{change.Kind}:{HashId(change.Device.Id)}", isFullscreenEligible: _settings.Current.ShowInFullscreen,
            audibleCue: change.Kind switch
            {
                DeviceHubChangeKind.Connected => AudibleNotificationCue.DeviceConnected,
                DeviceHubChangeKind.Disconnected => AudibleNotificationCue.DeviceDisconnected,
                DeviceHubChangeKind.BatteryLow => AudibleNotificationCue.LowBattery,
                _ => AudibleNotificationCue.None
            }));
    }

    private int ConnectedCount() => _service.Current.BluetoothDevices.Count(device =>
        device.ConnectionState == DeviceHubConnectionState.Connected) + _service.Current.StorageDevices.Count;

    private static string Glyph(DeviceHubDeviceCategory category) => category switch
    {
        DeviceHubDeviceCategory.AudioOutput => "\uE767",
        DeviceHubDeviceCategory.AudioInput => "\uE720",
        DeviceHubDeviceCategory.RemovableStorage => "\uE88E",
        _ => "\uE702"
    };

    private static string HashId(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..12];

    private ModuleCommandState? CreateNotificationCommand(DeviceHubChange change)
    {
        var kind = change.Kind switch
        {
            DeviceHubChangeKind.Connected when change.Device.Category == DeviceHubDeviceCategory.RemovableStorage => NotificationActionKind.OpenStorage,
            DeviceHubChangeKind.Connected when change.Device.Category == DeviceHubDeviceCategory.Bluetooth => NotificationActionKind.ManageBluetooth,
            DeviceHubChangeKind.DefaultAudioOutputChanged => NotificationActionKind.ManageSound,
            _ => NotificationActionKind.None
        };
        if (kind == NotificationActionKind.None)
        {
            return null;
        }

        var commandId = $"device-hub:{kind}:{HashId(change.Device.Id)}";
        if (_notificationActions.Count >= 32)
        {
            _notificationActions.Clear();
        }

        _notificationActions[commandId] = new NotificationAction(kind, change.Device);
        return new ModuleCommandState(
            commandId,
            kind switch
            {
                NotificationActionKind.OpenStorage => Text("DeviceHub.Open", "Aç"),
                NotificationActionKind.ManageBluetooth => Text("DeviceHub.ManageBluetooth", "Bluetooth ayarlarında yönet"),
                _ => Text("DeviceHub.ManageSound", "Ses ayarlarını aç")
            },
            kind == NotificationActionKind.OpenStorage ? "\uE8B7" : "\uE713",
            true);
    }

    private enum NotificationActionKind { None, OpenStorage, ManageBluetooth, ManageSound }

    private sealed record NotificationAction(NotificationActionKind Kind, DeviceHubDevice Device);
    private void OnLanguageChanged(object? sender, EventArgs e) => PresentationChanged?.Invoke(this, CurrentPresentation);
    private string Text(string key, string fallback) => _localization?.Get(key) is { } value && value != key ? value : fallback;

    public async ValueTask DisposeAsync()
    {
        _service.StateChanged -= OnStateChanged;
        _service.DeviceChanged -= OnDeviceChanged;
        if (_localization is not null) _localization.LanguageChanged -= OnLanguageChanged;
        await _service.DisposeAsync();
    }
}
