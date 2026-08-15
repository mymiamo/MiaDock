using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using MiaDock.Modules.Media.Models;
using MiaDock.Modules.Media.ViewModels;
using MiaDock.Modules.Notifications.Models;
using MiaDock.Modules.Notifications.Services;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.Services;
using MiaDock.Modules.Transfers.Services;
using System.ComponentModel;

namespace MiaDock.App.Services;

public sealed class ModuleSettingsCatalog : IDisposable
{
    private readonly MusicModuleViewModel _media;
    private readonly ISystemActivityService _systemActivity;
    private readonly IPrivacyUsageService _privacy;
    private readonly IPowerStatusService _power;
    private readonly INetworkStatusService _network;
    private readonly IBluetoothStatusService _bluetooth;
    private readonly ISystemNotificationService _notifications;
    private readonly ITransferProgressProvider _transfers;

    public ModuleSettingsCatalog(
        MusicModuleViewModel media,
        ISystemActivityService systemActivity,
        IPrivacyUsageService privacy,
        IPowerStatusService power,
        INetworkStatusService network,
        IBluetoothStatusService bluetooth,
        ISystemNotificationService notifications,
        ITransferProgressProvider transfers)
    {
        _media = media;
        _systemActivity = systemActivity;
        _privacy = privacy;
        _power = power;
        _network = network;
        _bluetooth = bluetooth;
        _notifications = notifications;
        _transfers = transfers;
        _media.PropertyChanged += OnMediaPropertyChanged;
        _systemActivity.SnapshotChanged += OnSnapshotChanged;
        _privacy.StateChanged += OnPrivacyStateChanged;
        _power.SnapshotChanged += OnSnapshotChanged;
        _network.SnapshotChanged += OnSnapshotChanged;
        _bluetooth.SnapshotChanged += OnSnapshotChanged;
        _notifications.AccessStateChanged += OnNotificationAccessChanged;
        _transfers.StateChanged += OnTransferStateChanged;
    }

    public event EventHandler? Changed;

    public ModuleAvailability GetAvailability(string moduleId, bool isEnabled)
    {
        if (!isEnabled)
        {
            return new(ModuleAvailabilityState.Disabled);
        }

        return moduleId switch
        {
            "media" => FromMedia(_media.ServiceState),
            "volume" => FromVolume(_systemActivity.Current),
            "privacy" => new(ModuleAvailabilityState.Ready),
            "system-activity" => FromSystemActivity(_systemActivity.Current.ServiceState),
            "battery" => FromBattery(_power.Current),
            "network" => FromDeviceState(_network.Current.State),
            "bluetooth" => FromDeviceState(_bluetooth.Current.State),
            "device-hub" => FromDeviceState(_bluetooth.Current.State),
            "notifications" => FromNotifications(_notifications.AccessState),
            "transfers" => FromTransfers(_transfers.State),
            _ => new(ModuleAvailabilityState.Ready)
        };
    }

    private static ModuleAvailability FromMedia(MediaServiceState state) => state switch
    {
        MediaServiceState.AccessDenied => new(ModuleAvailabilityState.PermissionDenied),
        MediaServiceState.Unavailable => new(ModuleAvailabilityState.ApiUnavailable),
        MediaServiceState.Faulted => new(ModuleAvailabilityState.TemporaryError),
        _ => new(ModuleAvailabilityState.Ready)
    };

    private static ModuleAvailability FromSystemActivity(SystemActivityServiceState state) => state switch
    {
        SystemActivityServiceState.Unavailable => new(ModuleAvailabilityState.ApiUnavailable),
        SystemActivityServiceState.Faulted => new(ModuleAvailabilityState.TemporaryError),
        _ => new(ModuleAvailabilityState.Ready)
    };

    private static ModuleAvailability FromVolume(SystemActivitySnapshot snapshot) =>
        snapshot.ServiceState switch
        {
            SystemActivityServiceState.Unavailable => new(ModuleAvailabilityState.ApiUnavailable),
            SystemActivityServiceState.Faulted => new(ModuleAvailabilityState.TemporaryError),
            _ when !snapshot.IsMasterVolumeAvailable =>
                new(ModuleAvailabilityState.NoCompatibleDevice),
            _ => new(ModuleAvailabilityState.Ready)
        };

    private static ModuleAvailability FromBattery(BatteryStatusSnapshot snapshot)
    {
        var state = FromDeviceState(snapshot.State);
        return state.State == ModuleAvailabilityState.Ready && !snapshot.IsBatteryPresent
            ? new(ModuleAvailabilityState.NoCompatibleDevice)
            : state;
    }

    private static ModuleAvailability FromDeviceState(DeviceServiceState state) => state switch
    {
        DeviceServiceState.Unavailable => new(ModuleAvailabilityState.ApiUnavailable),
        DeviceServiceState.Faulted => new(ModuleAvailabilityState.TemporaryError),
        _ => new(ModuleAvailabilityState.Ready)
    };

    private static ModuleAvailability FromNotifications(NotificationAccessState state) => state switch
    {
        NotificationAccessState.Unspecified or NotificationAccessState.Uninitialized =>
            new(ModuleAvailabilityState.PermissionRequired),
        NotificationAccessState.Denied => new(ModuleAvailabilityState.PermissionDenied),
        NotificationAccessState.Unsupported or NotificationAccessState.PackageIdentityRequired =>
            new(ModuleAvailabilityState.ApiUnavailable),
        NotificationAccessState.Faulted => new(ModuleAvailabilityState.TemporaryError),
        _ => new(ModuleAvailabilityState.Ready)
    };

    private static ModuleAvailability FromTransfers(TransferProviderState state) => state switch
    {
        TransferProviderState.Unavailable => new(ModuleAvailabilityState.ApiUnavailable),
        TransferProviderState.Faulted => new(ModuleAvailabilityState.TemporaryError),
        _ => new(ModuleAvailabilityState.Ready)
    };

    public void Dispose()
    {
        _media.PropertyChanged -= OnMediaPropertyChanged;
        _systemActivity.SnapshotChanged -= OnSnapshotChanged;
        _privacy.StateChanged -= OnPrivacyStateChanged;
        _power.SnapshotChanged -= OnSnapshotChanged;
        _network.SnapshotChanged -= OnSnapshotChanged;
        _bluetooth.SnapshotChanged -= OnSnapshotChanged;
        _notifications.AccessStateChanged -= OnNotificationAccessChanged;
        _transfers.StateChanged -= OnTransferStateChanged;
    }

    private void OnMediaPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MusicModuleViewModel.ServiceState)) Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnSnapshotChanged<T>(object? sender, T snapshot) => Changed?.Invoke(this, EventArgs.Empty);
    private void OnPrivacyStateChanged(object? sender, PrivacyState state) => Changed?.Invoke(this, EventArgs.Empty);
    private void OnNotificationAccessChanged(object? sender, NotificationAccessState state) => Changed?.Invoke(this, EventArgs.Empty);
    private void OnTransferStateChanged(object? sender, TransferProviderState state) => Changed?.Invoke(this, EventArgs.Empty);
}
