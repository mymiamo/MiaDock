using MiaDock.Core.Modules;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using MiaDock.Modules.DeviceStatus.ViewModels;

namespace MiaDock.Modules.DeviceStatus;

public sealed class NetworkModule : IIslandModule, IDisposable
{
    public const string ModuleId = "network";
    private readonly INetworkStatusService _service;
    private readonly NetworkModuleViewModel _viewModel;
    private NetworkStatusSnapshot? _previous;
    private bool _isEnabled = true;

    public NetworkModule(INetworkStatusService service, NetworkModuleViewModel viewModel)
    {
        _service = service;
        _viewModel = viewModel;
        _service.SnapshotChanged += OnSnapshotChanged;
    }

    public ModuleDescriptor Descriptor { get; } = new(
        ModuleId, "Ağ", 300, "NetworkCompactView", "NetworkExpandedView",
        new HashSet<ModuleEventKind> { ModuleEventKind.StatusChanged },
        TimeSpan.FromSeconds(3), notificationViewKey: "NetworkNotificationView",
        persistentPriority: 0, isPersistent: false, iconGlyph: "\uE701");

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

    private void OnSnapshotChanged(object? sender, NetworkStatusSnapshot current)
    {
        var previous = _previous;
        _previous = current;
        if (LifecycleState != ModuleLifecycleState.Active) return;
        PresentationChanged?.Invoke(this, CurrentPresentation);
        if (previous is null ||
            (previous.Connectivity == current.Connectivity && previous.ConnectionKind == current.ConnectionKind && previous.IsMetered == current.IsMetered))
        {
            return;
        }

        var priority = current.Connectivity == NetworkConnectivityKind.Offline
            ? ModuleEventPriority.High
            : ModuleEventPriority.Normal;
        EventOccurred?.Invoke(this, new ModuleEvent(
            ModuleId,
            ModuleEventKind.StatusChanged,
            CreatePresentation(current),
            Descriptor.DefaultDisplayDuration,
            DateTimeOffset.UtcNow,
            priority,
            "network:connectivity"));
    }

    private ModulePresentation CreatePresentation(NetworkStatusSnapshot snapshot) => new(
        ModuleId,
        _viewModel.ConnectivityText,
        snapshot.Connectivity == NetworkConnectivityKind.Offline
            ? "Bağlantı yok"
            : $"{_viewModel.ConnectionText} · {_viewModel.CostText}",
        _viewModel.NetworkGlyph,
        snapshot.Connectivity == NetworkConnectivityKind.Internet ? ModuleIndicatorKind.StatusDot : ModuleIndicatorKind.None,
        presentationKind: snapshot.Connectivity == NetworkConnectivityKind.Offline ? ModulePresentationKind.Alert : ModulePresentationKind.Status);

    public void Dispose() => _service.SnapshotChanged -= OnSnapshotChanged;
}
