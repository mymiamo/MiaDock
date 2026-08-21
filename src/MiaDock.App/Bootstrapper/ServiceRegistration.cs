using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using MiaDock.App.Modules;
using MiaDock.App.ViewModels;
using MiaDock.App.Infrastructure;
using MiaDock.Core.Input;
using MiaDock.Core.Modules;
using MiaDock.Core.Overlay;
using MiaDock.Core.Presentation;
using MiaDock.Core.Threading;
using MiaDock.Modules.Media;
using MiaDock.Modules.Media.Services;
using MiaDock.Modules.Media.ViewModels;
using MiaDock.UI.Presentation;
using MiaDock.UI.Services;
using MiaDock.Platform.Windows.Overlay;
using MiaDock.Platform.Windows.Media;
using MiaDock.App.Services;
using MiaDock.Core.Settings;
using MiaDock.Platform.Windows.Settings;
using MiaDock.Platform.Windows.Display;
using MiaDock.Platform.Windows.Fullscreen;
using MiaDock.Platform.Windows.Lifecycle;
using MiaDock.Platform.Windows.Startup;
using MiaDock.Platform.Windows.Tray;
using MiaDock.Core.Logging;
using MiaDock.Core.Audio;
using MiaDock.Core.Clipboard;
using MiaDock.Platform.Windows.Clipboard;
using MiaDock.Platform.Windows.Logging;
using MiaDock.Modules.SystemStatus;
using MiaDock.Modules.SystemStatus.Services;
using MiaDock.Modules.SystemStatus.Settings;
using MiaDock.Modules.SystemStatus.ViewModels;
using MiaDock.Platform.Windows.Audio;
using MiaDock.Platform.Windows.Privacy;
using MiaDock.Modules.DeviceStatus;
using MiaDock.Modules.DeviceStatus.Services;
using MiaDock.Modules.DeviceStatus.Settings;
using MiaDock.Modules.DeviceStatus.ViewModels;
using MiaDock.Platform.Windows.Power;
using MiaDock.Platform.Windows.Connectivity;
using MiaDock.Platform.Windows.Bluetooth;
using MiaDock.Modules.Time;
using MiaDock.Modules.Time.Services;
using MiaDock.Modules.Time.ViewModels;
using MiaDock.Platform.Windows.Time;
using MiaDock.Platform.Windows.HotKeys;
using MiaDock.Platform.Windows.Input;
using MiaDock.Modules.Notifications;
using MiaDock.Modules.Notifications.Services;
using MiaDock.Modules.Notifications.Settings;
using MiaDock.Platform.Windows.Notifications;
using MiaDock.Modules.Transfers;
using MiaDock.Modules.Transfers.Services;
using MiaDock.Modules.Transfers.Settings;
using MiaDock.Modules.Transfers.ViewModels;
using MiaDock.Platform.Windows.Transfers;
using MiaDock.Core.Localization;
using MiaDock.Core.Updates;
using MiaDock.Platform.Windows.Updates;
using MiaDock.Core.Focus;
using MiaDock.Core.Applications;
using MiaDock.Core.Lifecycle;
using MiaDock.Platform.Windows.Applications;

namespace MiaDock.App.Bootstrapper;

public static class ServiceRegistration
{
    public static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IIslandStateMachine, IslandStateMachine>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IOverlayPlacementCalculator, OverlayPlacementCalculator>();
        services.AddSingleton<IDisplayTopologyService, DisplayTopologyService>();
        services.AddSingleton<IOverlayWindowControllerFactory, OverlayWindowControllerFactory>();
        services.AddSingleton<IFullscreenDetectionService, WindowsFullscreenDetectionService>();
        services.AddSingleton<IApplicationActivityService, WindowsApplicationActivityService>();
        services.AddSingleton<IFocusSettingsLauncher, WindowsFocusSettingsLauncher>();
        services.AddSingleton<IExternalUriLauncher, WindowsExternalUriLauncher>();
        services.AddSingleton<ISingleInstanceService, WindowsSingleInstanceService>();
        services.AddSingleton<ICrashStateStore, JsonCrashStateStore>();
        services.AddSingleton<IWindowsSessionLockStateService, WindowsSessionLockStateService>();
        services.AddSingleton<IStartupTaskService, WindowsStartupTaskService>();
        services.AddSingleton<ITrayIconService>(_ => new WindowsTrayIconService(WindowBranding.IconPath));
        services.AddSingleton<IUiDispatcher, DispatcherQueueUiDispatcher>();
        services.AddSingleton<IClipboardPeekService, WindowsClipboardPeekService>();
        services.AddSingleton<MediaImageCache>();
        services.AddSingleton<IMediaSessionService, WindowsMediaSessionService>();
        services.AddSingleton<IMediaAudioMeterService, WindowsMediaAudioMeterService>();
        services.AddSingleton<ISystemActivityService, WindowsSystemActivityService>();
        services.AddSingleton<IAudioMixerService>(provider =>
            (IAudioMixerService)provider.GetRequiredService<ISystemActivityService>());
        services.AddSingleton<IPrivacyUsageService, WindowsPrivacyUsageService>();
        services.AddSingleton<IAudioSettingsLauncher, WindowsAudioSettingsLauncher>();
        services.AddSingleton<IAudioDeviceCatalog, WindowsAudioDeviceCatalog>();
        services.AddSingleton<IRemovableStorageService, WindowsRemovableStorageService>();
        services.AddSingleton<IDeviceHubSettingsLauncher, WindowsDeviceHubSettingsLauncher>();
        services.AddSingleton<IDeviceHubService, DeviceHubService>();
        services.AddSingleton<IPrivacySettingsLauncher, WindowsPrivacySettingsLauncher>();
        services.AddSingleton<IPowerStatusService, WindowsPowerStatusService>();
        services.AddSingleton<INetworkStatusService, WindowsNetworkStatusService>();
        services.AddSingleton<IRadioToggleService, WindowsRadioToggleService>();
        services.AddSingleton<IBluetoothRadioStateProvider, WindowsBluetoothRadioStateProvider>();
        services.AddSingleton<IBluetoothStatusService, WindowsBluetoothStatusService>();
        services.AddSingleton<IBluetoothDeviceConnectionService, WindowsBluetoothDeviceConnectionService>();
        services.AddSingleton<ISystemResumeService, WindowsSystemResumeService>();
        services.AddSingleton<ITimerStateStore, JsonTimerStateStore>();
        services.AddSingleton<ITimerAlarmPlayer, WindowsTimerAlarmPlayer>();
        services.AddSingleton<IAudibleNotificationPlayer, WindowsAudibleNotificationPlayer>();
        services.AddSingleton<ITimeToolsService, TimeToolsService>();
        services.AddSingleton<IGlobalHotKeyService, WindowsGlobalHotKeyService>();
        services.AddSingleton<ISystemNotificationService, WindowsSystemNotificationService>();
        services.AddSingleton<ITransferProgressProvider, WindowsTransferPipeServer>();
        services.AddSingleton<ITransferStateService, TransferStateService>();
        services.AddSingleton<IStoreUpdateService, WindowsStoreUpdateService>();
        services.AddSingleton<ModuleSettingsCatalog>();
        services.AddSingleton<PresentationPrivacyPolicy>();
        services.AddSingleton<IIslandModule, MusicModule>();
        services.AddSingleton<IIslandModule, VolumeModule>();
        services.AddSingleton<IIslandModule, PrivacyModule>();
        services.AddSingleton<IIslandModule, SystemActivityModule>();
        services.AddSingleton<IIslandModule, BatteryModule>();
        services.AddSingleton<IIslandModule, NetworkModule>();
        services.AddSingleton<IIslandModule, DeviceHubModule>();
        services.AddSingleton<IIslandModule, ClipboardPeekModule>();
        services.AddSingleton<IIslandModule, TimerModule>();
        services.AddSingleton<HourlyNotificationModule>();
        services.AddSingleton<IIslandModule>(provider =>
            provider.GetRequiredService<HourlyNotificationModule>());
        services.AddSingleton<IIslandModule, NotificationModule>();
        services.AddSingleton<IIslandModule, TransferModule>();
        services.AddSingleton<IKeyboardLockMonitor, WindowsKeyboardLockMonitor>();
        services.AddSingleton<KeyboardLockModule>();
        services.AddSingleton<IIslandModule>(provider =>
            provider.GetRequiredService<KeyboardLockModule>());
        services.AddSingleton<IUsbDeviceMonitor, WindowsUsbDeviceMonitor>();
        services.AddSingleton<UsbDeviceModule>();
        services.AddSingleton<IIslandModule>(provider =>
            provider.GetRequiredService<UsbDeviceModule>());
        services.AddSingleton<StoreUpdateModule>();
        services.AddSingleton<IIslandModule>(provider =>
            provider.GetRequiredService<StoreUpdateModule>());
        services.AddSingleton<IIslandModuleRegistry, IslandModuleRegistry>();
        services.AddSingleton<IModuleOrchestrator, ModuleOrchestrator>();
        services.AddSingleton<IModuleViewRegistry>(provider =>
        {
            var music = provider.GetRequiredService<MusicModuleViewModel>();
            var system = provider.GetRequiredService<SystemActivityViewModel>();
            var privacy = provider.GetRequiredService<PrivacyModuleViewModel>();
            var volume = provider.GetRequiredService<VolumeModuleViewModel>();
            var localization = provider.GetRequiredService<IAppLocalizationService>();
            var settings = provider.GetRequiredService<ISettingsService>();
            var focus = provider.GetRequiredService<FocusDockViewModel>();
            var registry = new ModuleViewRegistry();
            void Register(string key, Func<FrameworkElement> factory) =>
                registry.Register(key, () =>
                {
                    var view = factory();
                    view.Loaded += (_, _) => localization.Apply(view);
                    localization.Apply(view);
                    return view;
                });

            var idleDashboard = provider.GetRequiredService<IdleDashboardViewModel>();
            Register("EdgeRevealStatusView", () =>
                new Controls.EdgeRevealStatusView(music, system, privacy, localization, idleDashboard));
            Register("IdleCompactView", () =>
                new Controls.IdleCompactView(music, system, localization, settings, focus, privacy) { DataContext = idleDashboard });
            Register("IdleHoverView", () =>
                new Controls.IdleHoverView(music, idleDashboard, localization, settings, focus));
            Register("IdleExpandedView", () =>
                new Controls.IdleExpandedView(music, system, idleDashboard, localization, settings, focus));
            Register("MusicCompactView", () => new Controls.MusicCompactView { DataContext = music });
            Register("MusicHoverView", () => new Controls.MusicHoverView { DataContext = music });
            Register("MusicExpandedView", () => new Controls.ExpandedMusicView { DataContext = music });
            Register("MusicNotificationView", () => new Controls.TrackNotificationView { DataContext = music });
            Register("VolumeCompactView", () =>
                new Controls.VolumeCompactView { DataContext = volume });
            Register("VolumeExpandedView", () =>
                new Controls.VolumeExpandedView { DataContext = volume });
            Register("VolumeNotificationView", () =>
                new Controls.VolumeNotificationView { DataContext = volume });
            Register("PrivacyCompactView", () =>
                new Controls.PrivacyCompactView { DataContext = privacy });
            Register("PrivacyExpandedView", () =>
                new Controls.PrivacyExpandedView { DataContext = privacy });
            Register("SystemActivityCompactView", () =>
                new Controls.SystemActivityCompactView { DataContext = system });
            Register("SystemActivityExpandedView", () =>
                new Controls.SystemActivityExpandedView { DataContext = system });
            var battery = provider.GetRequiredService<BatteryModuleViewModel>();
            Register("BatteryCompactView", () => new Controls.BatteryCompactView { DataContext = battery });
            Register("BatteryExpandedView", () => new Controls.BatteryExpandedView { DataContext = battery });
            var network = provider.GetRequiredService<NetworkModuleViewModel>();
            Register("NetworkCompactView", () => new Controls.NetworkCompactView { DataContext = network });
            Register("NetworkExpandedView", () => new Controls.NetworkExpandedView { DataContext = network });
            var bluetooth = provider.GetRequiredService<BluetoothModuleViewModel>();
            Register("BluetoothCompactView", () => new Controls.BluetoothCompactView { DataContext = bluetooth });
            Register("BluetoothExpandedView", () => new Controls.BluetoothExpandedView { DataContext = bluetooth });
            var deviceHub = provider.GetRequiredService<DeviceHubViewModel>();
            Register("DeviceHubCompactView", () => new Controls.DeviceHubCompactView { DataContext = deviceHub });
            Register("DeviceHubExpandedView", () => new Controls.DeviceHubExpandedView { DataContext = deviceHub });
            Register("DeviceHubNotificationView", () => new Controls.DeviceHubNotificationView(
                provider.GetRequiredService<IIslandModuleRegistry>()));
            var clipboardPeek = provider.GetRequiredService<ClipboardPeekViewModel>();
            Register("ClipboardPeekCompactView", () => new Controls.ClipboardPeekCompactView { DataContext = clipboardPeek });
            Register("ClipboardPeekExpandedView", () => new Controls.ClipboardPeekExpandedView { DataContext = clipboardPeek });
            Register("ClipboardPeekNotificationView", () => new Controls.ClipboardPeekNotificationView(
                provider.GetRequiredService<IIslandModuleRegistry>(), clipboardPeek));
            var timeTools = provider.GetRequiredService<TimeToolsViewModel>();
            Register("TimerCompactView", () => new Controls.TimerCompactView { DataContext = timeTools });
            Register("TimerHoverView", () => new Controls.TimerHoverView { DataContext = timeTools });
            Register("TimerExpandedView", () => new Controls.TimerExpandedView(localization) { DataContext = timeTools });
            Register("TimerNotificationView", () => new Controls.TimerNotificationView { DataContext = timeTools });
            Register("NotificationModuleNotificationView", () => new Controls.NotificationModuleNotificationView());
            var transfers = provider.GetRequiredService<TransferModuleViewModel>();
            Register("TransferCompactView", () =>
                new Controls.TransferCompactView { DataContext = transfers });
            Register("TransferExpandedView", () =>
                new Controls.TransferExpandedView { DataContext = transfers });
            Register("TransferNotificationView", () => new Controls.TransferNotificationView());
            Register("StoreUpdateNotificationView", () =>
                new Controls.StoreUpdateNotificationView(
                    provider.GetRequiredService<IStoreUpdateService>()));
            return registry;
        });
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<AppLocalizationService>();
        services.AddSingleton<IAppLocalizationService>(provider =>
            provider.GetRequiredService<AppLocalizationService>());
        services.AddSingleton<ILocalizationService>(provider =>
            provider.GetRequiredService<AppLocalizationService>());
        services.AddSingleton<IAnimationPreferenceService, WindowsAnimationPreferenceService>();
        services.AddSingleton<ISettingsPathProvider, SettingsPathProvider>();
        services.AddSingleton<ILogPathProvider, LocalLogPathProvider>();
        services.AddSingleton<SensitiveDataRedactor>();
        services.AddSingleton(LogRetentionPolicy.Default);
        services.AddSingleton<JsonLinesLogService>();
        services.AddSingleton<ILogService>(provider => provider.GetRequiredService<JsonLinesLogService>());
        services.AddSingleton<ILogReader>(provider => provider.GetRequiredService<JsonLinesLogService>());
        services.AddSingleton<ILogArchiveService>(provider => provider.GetRequiredService<JsonLinesLogService>());
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<AudibleNotificationSettingsProvider>();
        services.AddSingleton<IAudibleNotificationSettingsProvider>(provider =>
            provider.GetRequiredService<AudibleNotificationSettingsProvider>());
        services.AddSingleton<OverlayWindowHandleProvider>();
        services.AddSingleton<IOverlayWindowHandleProvider>(provider => provider.GetRequiredService<OverlayWindowHandleProvider>());
        services.AddSingleton<FocusService>();
        services.AddSingleton<IFocusService>(provider =>
            provider.GetRequiredService<FocusService>());
        services.AddSingleton<FocusPolicyService>();
        services.AddSingleton<IFocusPolicyService>(provider =>
            provider.GetRequiredService<FocusPolicyService>());
        services.AddSingleton<FocusAutomationService>();
        services.AddSingleton<IFocusAutomationService>(provider =>
            provider.GetRequiredService<FocusAutomationService>());
        services.AddSingleton<BatteryModuleSettingsAdapter>();
        services.AddSingleton<IBatteryModuleSettings>(provider => provider.GetRequiredService<BatteryModuleSettingsAdapter>());
        services.AddSingleton<DeviceHubSettingsAdapter>();
        services.AddSingleton<IDeviceHubSettings>(provider => provider.GetRequiredService<DeviceHubSettingsAdapter>());
        services.AddSingleton<ClipboardPeekSettingsAdapter>();
        services.AddSingleton<IClipboardPeekSettings>(provider => provider.GetRequiredService<ClipboardPeekSettingsAdapter>());
        services.AddSingleton<VolumeModuleSettingsAdapter>();
        services.AddSingleton<IVolumeModuleSettings>(provider =>
            provider.GetRequiredService<VolumeModuleSettingsAdapter>());
        services.AddSingleton<NotificationModuleSettingsAdapter>();
        services.AddSingleton<INotificationModuleSettings>(provider => provider.GetRequiredService<NotificationModuleSettingsAdapter>());
        services.AddSingleton<TransferModuleSettingsAdapter>();
        services.AddSingleton<ITransferModuleSettings>(provider => provider.GetRequiredService<TransferModuleSettingsAdapter>());

        services.AddSingleton<MusicModuleViewModel>();
        services.AddSingleton<SystemActivityViewModel>();
        services.AddSingleton<PrivacyModuleViewModel>();
        services.AddSingleton<VolumeModuleViewModel>();
        services.AddSingleton<BatteryModuleViewModel>();
        services.AddSingleton<NetworkModuleViewModel>();
        services.AddSingleton<BluetoothModuleViewModel>();
        services.AddSingleton<DeviceHubViewModel>();
        services.AddSingleton<ClipboardPeekViewModel>();
        services.AddSingleton<TimeToolsViewModel>();
        services.AddSingleton<TransferModuleViewModel>();
        services.AddSingleton<IdleDashboardViewModel>();
        services.AddSingleton<FocusDockViewModel>();
        services.AddSingleton<FocusSettingsViewModel>();
        services.AddSingleton<IslandViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<OverlayWindowViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<OnboardingViewModel>();
        services.AddSingleton<DiagnosticsViewModel>();
        services.AddSingleton<IDiagnosticsFileService, DiagnosticsFileService>();
        services.AddSingleton<AppExceptionCoordinator>();
        services.AddSingleton<CrashRecoveryCoordinator>();
        services.AddSingleton<ISettingsWindowService, SettingsWindowService>();
        services.AddSingleton<IOnboardingWindowService, OnboardingWindowService>();
        services.AddSingleton<IApplicationLifetimeService, ApplicationLifetimeService>();
        services.AddSingleton<OverlayWindow>();
        services.AddSingleton<IOverlayWindowService, OverlayWindowService>();
        services.AddSingleton<TrayMenuCoordinator>();
        services.AddSingleton<GlobalHotKeyCoordinator>();
        services.AddSingleton<ModuleSettingsCoordinator>();
        services.AddSingleton<StoreUpdateCoordinator>();
        services.AddSingleton<StartupTaskCoordinator>();
        services.AddSingleton<IStoreUpdateCoordinator>(provider =>
            provider.GetRequiredService<StoreUpdateCoordinator>());

        return services.BuildServiceProvider(validateScopes: true);
    }
}
