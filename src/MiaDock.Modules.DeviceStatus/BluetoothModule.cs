using System.Security.Cryptography;
using System.Text;
using MiaDock.Core.Modules;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using MiaDock.Modules.DeviceStatus.ViewModels;

namespace MiaDock.Modules.DeviceStatus;

public sealed class BluetoothModule : IIslandModule, IDisposable
{
    public const string ModuleId = "bluetooth";
    private readonly IBluetoothStatusService _service;
    private readonly BluetoothModuleViewModel _viewModel;
    private BluetoothStatusSnapshot? _previous;
    private bool _isEnabled = true;

    public BluetoothModule(IBluetoothStatusService service, BluetoothModuleViewModel viewModel)
    {
        _service = service;
        _viewModel = viewModel;
        _service.SnapshotChanged += OnSnapshotChanged;
    }

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleId, "Bluetooth", 250, "BluetoothCompactView", "BluetoothExpandedView",
        new HashSet<ModuleEventKind> { ModuleEventKind.StatusChanged },
        TimeSpan.FromSeconds(3), notificationViewKey: "BluetoothNotificationView",
        persistentPriority: 0, isPersistent: false, iconGlyph: "\uE702");

    public ModuleLifecycleState LifecycleState { get; private set; }
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; PresentationChanged?.Invoke(this, CurrentPresentation); } }
    public ModulePresentation? CurrentPresentation => LifecycleState == ModuleLifecycleState.Active
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

    private void OnSnapshotChanged(object? sender, BluetoothStatusSnapshot current)
    {
        var previous = _previous;
        _previous = current;
        if (LifecycleState != ModuleLifecycleState.Active) return;
        PresentationChanged?.Invoke(this, CurrentPresentation);
        if (previous is null || !previous.IsEnumerationComplete || !current.IsEnumerationComplete) return;

        var previousById = previous.Devices.ToDictionary(device => device.Id, StringComparer.Ordinal);
        var currentById = current.Devices.ToDictionary(device => device.Id, StringComparer.Ordinal);
        var changed = current.Devices.FirstOrDefault(device =>
            !previousById.TryGetValue(device.Id, out var old) || old.IsConnected != device.IsConnected);
        changed ??= previous.Devices.FirstOrDefault(device =>
            device.IsConnected && !currentById.ContainsKey(device.Id)) is { } removed
                ? removed with { IsConnected = false }
                : null;
        if (changed is null) return;

        var presentation = new ModulePresentation(
            ModuleId,
            changed.IsConnected ? "Bluetooth cihazı bağlandı" : "Bluetooth cihazı ayrıldı",
            changed.DisplayName,
            "\uE702",
            ModuleIndicatorKind.StatusDot,
            isSensitive: true,
            presentationKind: ModulePresentationKind.Status);
        EventOccurred?.Invoke(this, new ModuleEvent(
            ModuleId,
            ModuleEventKind.StatusChanged,
            presentation,
            Descriptor.DefaultDisplayDuration,
            DateTimeOffset.UtcNow,
            ModuleEventPriority.Normal,
            $"bluetooth:device:{HashId(changed.Id)}",
            isFullscreenEligible: false));
    }

    private ModulePresentation CreatePresentation(BluetoothStatusSnapshot snapshot) => new(
        ModuleId,
        "Bluetooth",
        _viewModel.StatusText,
        "\uE702",
        _viewModel.ConnectedDevices.Count > 0 ? ModuleIndicatorKind.StatusDot : ModuleIndicatorKind.None,
        valueText: _viewModel.ConnectedDevices.Count > 0 ? _viewModel.ConnectedDevices.Count.ToString() : null,
        presentationKind: ModulePresentationKind.Status);

    private static string HashId(string id) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)))[..12];

    public void Dispose() => _service.SnapshotChanged -= OnSnapshotChanged;
}
