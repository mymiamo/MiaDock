namespace MiaDock.Core.Logging;

public static class TechnicalEventIds
{
    public const string ApplicationStarting = "app.starting";
    public const string ApplicationReady = "app.ready";
    public const string ApplicationStopping = "app.stopping";
    public const string ApplicationUnhandled = "app.unhandled";
    public const string AppDomainUnhandled = "runtime.unhandled";
    public const string UnobservedTask = "runtime.unobserved-task";
    public const string SettingsLoadFailed = "settings.load-failed";
    public const string SettingsSaveFailed = "settings.save-failed";
    public const string MediaSelectionFailed = "media.selection-failed";
    public const string SystemActivityReady = "system-activity.ready";
    public const string CameraWatcherUnavailable = "system-activity.camera-watcher-unavailable";
    public const string PowerStatusReady = "device-status.power-ready";
    public const string NetworkStatusReady = "device-status.network-ready";
    public const string NetworkCountersUnavailable = "device-status.network-counters-unavailable";
    public const string BluetoothWatcherReady = "device-status.bluetooth-ready";
    public const string DeviceStatusUnavailable = "device-status.unavailable";
    public const string TrayCommandFailed = "tray.command-failed";
    public const string LogsCleared = "logs.cleared";
    public const string LogExportFailed = "logs.export-failed";
    public const string StoreUpdateCheckCompleted = "store-update.check-completed";
    public const string StoreUpdateCheckFailed = "store-update.check-failed";
}
