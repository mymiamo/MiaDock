using Microsoft.Extensions.DependencyInjection;
using MiaDock.App.ViewModels;
using MiaDock.App.Infrastructure;
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
using MiaDock.Platform.Windows.Logging;
using MiaDock.Modules.SystemStatus;
using MiaDock.Modules.SystemStatus.Services;
using MiaDock.Modules.SystemStatus.ViewModels;
using MiaDock.Platform.Windows.Audio;
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
using MiaDock.Modules.Notifications;
using MiaDock.Modules.Notifications.Services;
using MiaDock.Modules.Notifications.Settings;
using MiaDock.Platform.Windows.Notifications;
using MiaDock.Modules.Transfers;
using MiaDock.Modules.Transfers.Services;
using MiaDock.Modules.Transfers.Settings;
using MiaDock.Modules.Transfers.ViewModels;
using MiaDock.Platform.Windows.Transfers;

namespace MiaDock.App.Bootstrapper;

public static class ServiceRegistration
{
    public static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IIslandStateMachine, IslandStateMachine>();
        services.AddSingleton<IOverlayPlacementCalculator, OverlayPlacementCalculator>();
        services.AddSingleton<IDisplayTopologyService, DisplayTopologyService>();
        services.AddSingleton<IOverlayWindowControllerFactory, OverlayWindowControllerFactory>();
        services.AddSingleton<IFullscreenDetectionService, WindowsFullscreenDetectionService>();
        services.AddSingleton<ISingleInstanceService, WindowsSingleInstanceService>();
        services.AddSingleton<IWindowsSessionLockStateService, WindowsSessionLockStateService>();
        services.AddSingleton<IStartupTaskService, WindowsStartupTaskService>();
        services.AddSingleton<ITrayIconService>(_ => new WindowsTrayIconService(WindowBranding.IconPath));
        services.AddSingleton<IUiDispatcher, DispatcherQueueUiDispatcher>();
        services.AddSingleton<MediaImageCache>();
        services.AddSingleton<IMediaSessionService, WindowsMediaSessionService>();
        services.AddSingleton<IMediaAudioMeterService, WindowsMediaAudioMeterService>();
        services.AddSingleton<ISystemActivityService, WindowsSystemActivityService>();
        services.AddSingleton<IPowerStatusService, WindowsPowerStatusService>();
        services.AddSingleton<INetworkStatusService, WindowsNetworkStatusService>();
        services.AddSingleton<IBluetoothStatusService, WindowsBluetoothStatusService>();
        services.AddSingleton<ISystemResumeService, WindowsSystemResumeService>();
        services.AddSingleton<ITimerStateStore, JsonTimerStateStore>();
        services.AddSingleton<ITimerAlarmPlayer, WindowsTimerAlarmPlayer>();
        services.AddSingleton<ITimeToolsService, TimeToolsService>();
        services.AddSingleton<IGlobalHotKeyService, WindowsGlobalHotKeyService>();
        services.AddSingleton<ISystemNotificationService, WindowsSystemNotificationService>();
        services.AddSingleton<ITransferProgressProvider, WindowsTransferPipeServer>();
        services.AddSingleton<ITransferStateService, TransferStateService>();
        services.AddSingleton<ModuleSettingsCatalog>();
        services.AddSingleton<PresentationPrivacyPolicy>();
        services.AddSingleton<IIslandModule, MusicModule>();
        services.AddSingleton<IIslandModule, SystemActivityModule>();
        services.AddSingleton<IIslandModule, BatteryModule>();
        services.AddSingleton<IIslandModule, NetworkModule>();
        services.AddSingleton<IIslandModule, BluetoothModule>();
        services.AddSingleton<IIslandModule, TimerModule>();
        services.AddSingleton<IIslandModule, NotificationModule>();
        services.AddSingleton<IIslandModule, TransferModule>();
        services.AddSingleton<IIslandModuleRegistry, IslandModuleRegistry>();
        services.AddSingleton<IModuleOrchestrator, ModuleOrchestrator>();
        services.AddSingleton<IModuleViewRegistry>(provider =>
        {
            var music = provider.GetRequiredService<MusicModuleViewModel>();
            var system = provider.GetRequiredService<SystemActivityViewModel>();
            var registry = new ModuleViewRegistry();
            var idleDashboard = provider.GetRequiredService<IdleDashboardViewModel>();
            registry.Register("IdleCompactView", () =>
                new Controls.IdleCompactView(music, system) { DataContext = idleDashboard });
            registry.Register("IdleHoverView", () =>
                new Controls.IdleHoverView(music, idleDashboard));
            registry.Register("MusicCompactView", () => new Controls.MusicCompactView { DataContext = music });
            registry.Register("MusicHoverView", () => new Controls.MusicHoverView { DataContext = music });
            registry.Register("MusicExpandedView", () => new Controls.ExpandedMusicView { DataContext = music });
            registry.Register("MusicNotificationView", () => new Controls.TrackNotificationView { DataContext = music });
            registry.Register("SystemActivityCompactView", () =>
                new Controls.SystemActivityCompactView { DataContext = system });
            registry.Register("SystemActivityExpandedView", () =>
                new Controls.SystemActivityExpandedView { DataContext = system });
            var battery = provider.GetRequiredService<BatteryModuleViewModel>();
            registry.Register("BatteryCompactView", () => new Controls.BatteryCompactView { DataContext = battery });
            registry.Register("BatteryExpandedView", () => new Controls.BatteryExpandedView { DataContext = battery });
            var network = provider.GetRequiredService<NetworkModuleViewModel>();
            registry.Register("NetworkCompactView", () => new Controls.NetworkCompactView { DataContext = network });
            registry.Register("NetworkExpandedView", () => new Controls.NetworkExpandedView { DataContext = network });
            var bluetooth = provider.GetRequiredService<BluetoothModuleViewModel>();
            registry.Register("BluetoothCompactView", () => new Controls.BluetoothCompactView { DataContext = bluetooth });
            registry.Register("BluetoothExpandedView", () => new Controls.BluetoothExpandedView { DataContext = bluetooth });
            var timeTools = provider.GetRequiredService<TimeToolsViewModel>();
            registry.Register("TimerCompactView", () => new Controls.TimerCompactView { DataContext = timeTools });
            registry.Register("TimerHoverView", () => new Controls.TimerHoverView { DataContext = timeTools });
            registry.Register("TimerExpandedView", () => new Controls.TimerExpandedView { DataContext = timeTools });
            registry.Register("TimerNotificationView", () => new Controls.TimerNotificationView { DataContext = timeTools });
            registry.Register("NotificationModuleNotificationView", () => new Controls.NotificationModuleNotificationView());
            var transfers = provider.GetRequiredService<TransferModuleViewModel>();
            registry.Register("TransferCompactView", () =>
                new Controls.TransferCompactView { DataContext = transfers });
            registry.Register("TransferExpandedView", () =>
                new Controls.TransferExpandedView { DataContext = transfers });
            registry.Register("TransferNotificationView", () => new Controls.TransferNotificationView());
            return registry;
        });
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IAppLocalizationService, AppLocalizationService>();
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
        services.AddSingleton<BatteryModuleSettingsAdapter>();
        services.AddSingleton<IBatteryModuleSettings>(provider => provider.GetRequiredService<BatteryModuleSettingsAdapter>());
        services.AddSingleton<NotificationModuleSettingsAdapter>();
        services.AddSingleton<INotificationModuleSettings>(provider => provider.GetRequiredService<NotificationModuleSettingsAdapter>());
        services.AddSingleton<TransferModuleSettingsAdapter>();
        services.AddSingleton<ITransferModuleSettings>(provider => provider.GetRequiredService<TransferModuleSettingsAdapter>());

        services.AddSingleton<MusicModuleViewModel>();
        services.AddSingleton<SystemActivityViewModel>();
        services.AddSingleton<BatteryModuleViewModel>();
        services.AddSingleton<NetworkModuleViewModel>();
        services.AddSingleton<BluetoothModuleViewModel>();
        services.AddSingleton<TimeToolsViewModel>();
        services.AddSingleton<TransferModuleViewModel>();
        services.AddSingleton<IdleDashboardViewModel>();
        services.AddSingleton<IslandViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        services.AddSingleton<OverlayWindowViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<OnboardingViewModel>();
        services.AddSingleton<DiagnosticsViewModel>();
        services.AddSingleton<IDiagnosticsFileService, DiagnosticsFileService>();
        services.AddSingleton<AppExceptionCoordinator>();
        services.AddSingleton<ISettingsWindowService, SettingsWindowService>();
        services.AddSingleton<IOnboardingWindowService, OnboardingWindowService>();
        services.AddSingleton<IApplicationLifetimeService, ApplicationLifetimeService>();
        services.AddSingleton<OverlayWindow>();
        services.AddSingleton<TrayMenuCoordinator>();
        services.AddSingleton<GlobalHotKeyCoordinator>();
        services.AddSingleton<ModuleSettingsCoordinator>();

        return services.BuildServiceProvider(validateScopes: true);
    }
}
