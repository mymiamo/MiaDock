using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiaDock.App.Services;
using MiaDock.Core.Presentation;
using MiaDock.Core.Settings;
using MiaDock.Core.Theming;
using MiaDock.Modules.Media.Models;
using MiaDock.Modules.Media.ViewModels;
using MiaDock.Platform.Windows.Display;
using MiaDock.Platform.Windows.Startup;
using MiaDock.Modules.DeviceStatus.Settings;
using MiaDock.Platform.Windows.HotKeys;
using System.Collections.ObjectModel;
using MiaDock.Core.Threading;
using MiaDock.Modules.Notifications.Models;
using MiaDock.Modules.Notifications.Services;
using MiaDock.Modules.Notifications.Settings;
using MiaDock.Core.Updates;

namespace MiaDock.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly MusicModuleViewModel _music;
    private readonly IDisplayTopologyService? _displayTopology;
    private readonly IStartupTaskService? _startupTaskService;
    private readonly IGlobalHotKeyService? _hotKeyService;
    private readonly ISystemNotificationService? _notificationService;
    private readonly IUiDispatcher? _uiDispatcher;
    private readonly IAppLocalizationService _localization;
    private readonly ModuleSettingsCatalog? _moduleCatalog;
    private readonly IStoreUpdateCoordinator? _storeUpdates;
    private bool _synchronizing;
    private AppLanguage _language;
    private IslandVisibilityMode _visibilityMode;
    private IslandInteractionMode _interactionMode;
    private IslandPositionSetting _position;
    private double _passiveModuleReturnSeconds;
    private ClockHourFormat _clockHourFormat;
    private bool _showClockSeconds;
    private bool _showClockDate;
    private ClockDateFormat _clockDateFormat;
    private bool _showClockWeekday;
    private ThemeStyle _theme;
    private double _collapsedWidth;
    private double _collapsedHeight;
    private double _hoverWidth;
    private double _hoverHeight;
    private double _expandedWidth;
    private double _expandedHeight;
    private double _notificationWidth;
    private double _notificationHeight;
    private double _cornerRadius;
    private string _backgroundColor = string.Empty;
    private string _accentColor = string.Empty;
    private double _opacity;
    private double _shadowIntensity;
    private double _animationSpeed;
    private IslandAnimationKind _animationKind;
    private string? _selectedSourceId;
    private MediaFallbackSetting _mediaFallback;
    private VolumeTargetSetting _volumeTarget;
    private bool _fullscreenEnabled;
    private double _fullscreenNotificationSeconds;
    private FullscreenNotificationStyle _fullscreenStyle;
    private bool _showTrackChanges;
    private MonitorSelectionMode _monitorMode;
    private string? _fixedMonitorId;
    private bool _showTrayIcon;
    private bool _showTrayMediaControls;
    private bool _temporaryNotifications;
    private bool _startWithWindows;
    private StartupLaunchMode _launchMode;
    private CloseBehaviorSetting _closeBehavior;
    private bool _isStartupTaskAvailable;
    private string _startupStatusMessage = "Başlangıç durumu denetleniyor.";
    private double _batteryLowThreshold;
    private double _batteryCriticalThreshold;
    private double _batteryEmergencyThreshold;
    private bool _hotKeysEnabled;
    private HotKeyGestureSetting? _toggleDockHotKey;
    private HotKeyGestureSetting? _toggleExpandedHotKey;
    private HotKeyGestureSetting? _nextModuleHotKey;
    private HotKeyGestureSetting? _mediaPlayPauseHotKey;
    private HotKeyGestureSetting? _timerPauseResumeHotKey;
    private string _hotKeyStatusMessage = "Global kısayollar kapalı.";
    private bool _notificationsEnabled;
    private double _notificationEventSeconds = 5;
    private bool _notificationsInFullscreen;
    private bool _notificationUseAllowList;
    private NotificationAccessState _notificationAccessState;
    private string _notificationStatusMessage = "Bildirim erişimi denetleniyor.";
    private bool _showSensitiveContentInFullscreen;
    private bool _showSensitiveContentWhenLocked;
    private bool _automaticUpdateChecksEnabled = true;
    private StoreUpdateSnapshot _storeUpdateSnapshot =
        StoreUpdateSnapshot.Unavailable(new Version(1, 1, 0, 0));

    public SettingsViewModel(
        ISettingsService settingsService,
        MusicModuleViewModel music,
        IDisplayTopologyService? displayTopology = null,
        IStartupTaskService? startupTaskService = null,
        IGlobalHotKeyService? hotKeyService = null,
        ISystemNotificationService? notificationService = null,
        IUiDispatcher? uiDispatcher = null,
        IAppLocalizationService? localization = null,
        ModuleSettingsCatalog? moduleCatalog = null,
        IStoreUpdateCoordinator? storeUpdates = null)
    {
        _settingsService = settingsService;
        _music = music;
        _displayTopology = displayTopology;
        _startupTaskService = startupTaskService;
        _hotKeyService = hotKeyService;
        _notificationService = notificationService;
        _uiDispatcher = uiDispatcher;
        _localization = localization ?? new AppLocalizationService();
        _moduleCatalog = moduleCatalog;
        _storeUpdates = storeUpdates;
        _storeUpdateSnapshot = storeUpdates?.Current ?? _storeUpdateSnapshot;
        BuildModuleItems();
        RebuildLocalizedOptions();
        LoadFrom(settingsService.Current);
        _settingsService.SettingsChanged += OnSettingsChanged;
        _music.PropertyChanged += OnMusicPropertyChanged;
        if (_displayTopology is not null)
        {
            _displayTopology.DisplaysChanged += OnDisplaysChanged;
        }
        if (_hotKeyService is not null)
        {
            _hotKeyService.RegistrationsChanged += OnHotKeyRegistrationsChanged;
        }
        if (_notificationService is not null)
        {
            _notificationService.AccessStateChanged += OnNotificationAccessStateChanged;
            _notificationService.SourcesChanged += OnNotificationSourcesChanged;
        }
        if (_moduleCatalog is not null)
        {
            _moduleCatalog.Changed += OnModuleCatalogChanged;
        }
        ResetAllCommand = new RelayCommand(_settingsService.Reset);
        ResetAppearanceCommand = new RelayCommand(() =>
            ApplyAppearanceAndSave(AppearanceSettings.Default));
        CheckForUpdatesCommand = new AsyncRelayCommand(
            CheckForUpdatesAsync,
            () => !IsStoreUpdateChecking);
        OpenStoreCommand = new AsyncRelayCommand(
            OpenStoreAsync,
            () => IsStoreUpdateAvailable);
        if (_storeUpdates is not null)
        {
            _storeUpdates.UpdateAvailabilityChanged += OnStoreUpdateAvailabilityChanged;
        }
        _ = RefreshStartupStatusAsync();
    }

    public IReadOnlyList<SettingOption<AppLanguage>> Languages { get; private set; } = [];
    public IReadOnlyList<SettingOption<IslandVisibilityMode>> VisibilityModes { get; private set; } = [];
    public IReadOnlyList<SettingOption<IslandInteractionMode>> InteractionModes { get; private set; } = [];
    public IReadOnlyList<SettingOption<IslandPositionSetting>> Positions { get; private set; } = [];
    public IReadOnlyList<SettingOption<ClockHourFormat>> ClockHourFormats { get; private set; } = [];
    public IReadOnlyList<SettingOption<ClockDateFormat>> ClockDateFormats { get; private set; } = [];
    public IReadOnlyList<SettingOption<ThemeStyle>> Themes { get; private set; } = [];
    public IReadOnlyList<SettingOption<IslandAnimationKind>> AnimationKinds { get; private set; } = [];
    public IReadOnlyList<SettingOption<MediaFallbackSetting>> MediaFallbackModes { get; private set; } = [];
    public IReadOnlyList<SettingOption<VolumeTargetSetting>> VolumeTargets { get; private set; } = [];
    public IReadOnlyList<SettingOption<FullscreenNotificationStyle>> FullscreenStyles { get; private set; } = [];
    public IReadOnlyList<SettingOption<MonitorSelectionMode>> MonitorModes { get; private set; } = [];
    public IReadOnlyList<SettingOption<StartupLaunchMode>> LaunchModes { get; private set; } = [];
    public IReadOnlyList<SettingOption<CloseBehaviorSetting>> CloseBehaviors { get; private set; } = [];

    public IReadOnlyList<MediaSourceInfo> MediaSources => _music.Sources;
    public IReadOnlyList<DisplayDescriptor> Displays => _displayTopology?.Displays ?? Array.Empty<DisplayDescriptor>();
    public string VersionText => Assembly.GetEntryAssembly()?.GetName().Version?.ToString(4) ?? "1.1.1.0";
    public string SettingsFilePath => _settingsService.SettingsFilePath;
    public IRelayCommand ResetAllCommand { get; }
    public IRelayCommand ResetAppearanceCommand { get; }
    public IAsyncRelayCommand CheckForUpdatesCommand { get; }
    public IAsyncRelayCommand OpenStoreCommand { get; }
    public bool IsStartupTaskAvailable { get => _isStartupTaskAvailable; private set => SetProperty(ref _isStartupTaskAvailable, value); }
    public string StartupStatusMessage { get => _startupStatusMessage; private set => SetProperty(ref _startupStatusMessage, value); }
    public double BatteryLowThreshold { get => _batteryLowThreshold; set => SetBatteryThreshold(value, ref _batteryLowThreshold, ThresholdKind.Low); }
    public double BatteryCriticalThreshold { get => _batteryCriticalThreshold; set => SetBatteryThreshold(value, ref _batteryCriticalThreshold, ThresholdKind.Critical); }
    public double BatteryEmergencyThreshold { get => _batteryEmergencyThreshold; set => SetBatteryThreshold(value, ref _batteryEmergencyThreshold, ThresholdKind.Emergency); }
    public bool HotKeysEnabled { get => _hotKeysEnabled; set => Set(value, ref _hotKeysEnabled, s => s with { HotKeys = s.HotKeys with { IsEnabled = value } }); }
    public HotKeyGestureSetting? ToggleDockHotKey { get => _toggleDockHotKey; set => SetHotKey(HotKeyAction.ToggleDock, value, ref _toggleDockHotKey); }
    public HotKeyGestureSetting? ToggleExpandedHotKey { get => _toggleExpandedHotKey; set => SetHotKey(HotKeyAction.ToggleExpanded, value, ref _toggleExpandedHotKey); }
    public HotKeyGestureSetting? NextModuleHotKey { get => _nextModuleHotKey; set => SetHotKey(HotKeyAction.NextModule, value, ref _nextModuleHotKey); }
    public HotKeyGestureSetting? MediaPlayPauseHotKey { get => _mediaPlayPauseHotKey; set => SetHotKey(HotKeyAction.MediaPlayPause, value, ref _mediaPlayPauseHotKey); }
    public HotKeyGestureSetting? TimerPauseResumeHotKey { get => _timerPauseResumeHotKey; set => SetHotKey(HotKeyAction.TimerPauseResume, value, ref _timerPauseResumeHotKey); }
    public string HotKeyStatusMessage { get => _hotKeyStatusMessage; private set => SetProperty(ref _hotKeyStatusMessage, value); }
    public bool NotificationsEnabled
    {
        get => _notificationsEnabled;
        private set
        {
            if (SetProperty(ref _notificationsEnabled, value))
            {
                OnPropertyChanged(nameof(CanEnableNotifications));
            }
        }
    }
    public double NotificationEventSeconds { get => _notificationEventSeconds; set => SetNotificationOptions(value, ref _notificationEventSeconds, options => options with { EventDuration = TimeSpan.FromSeconds(value) }); }
    public bool NotificationsInFullscreen { get => _notificationsInFullscreen; set => SetNotificationOptions(value, ref _notificationsInFullscreen, options => options with { ShowInFullscreen = value }); }
    public bool NotificationUseAllowList { get => _notificationUseAllowList; set => SetNotificationOptions(value, ref _notificationUseAllowList, options => options with { UseAllowList = value }); }
    public NotificationAccessState NotificationAccessState { get => _notificationAccessState; private set { if (SetProperty(ref _notificationAccessState, value)) UpdateNotificationStatus(); } }
    public string NotificationStatusMessage { get => _notificationStatusMessage; private set => SetProperty(ref _notificationStatusMessage, value); }
    public bool CanEnableNotifications => NotificationsEnabled ||
        NotificationAccessState is NotificationAccessState.Allowed or NotificationAccessState.Unspecified;
    public ObservableCollection<NotificationApplicationSettingItem> NotificationApplications { get; } = [];
    public ObservableCollection<ModuleSettingsItemViewModel> ModuleItems { get; } = [];
    public int EnabledModuleCount => ModuleItems.Count(item => item.IsEnabled);
    public string EnabledModuleSummary => _localization.Text(
        $"{EnabledModuleCount} / {ModuleItems.Count} modül etkin",
        $"{EnabledModuleCount} of {ModuleItems.Count} modules enabled");
    public bool ShowSensitiveContentInFullscreen
    {
        get => _showSensitiveContentInFullscreen;
        set => Set(value, ref _showSensitiveContentInFullscreen,
            settings => settings with
            {
                Privacy = settings.Privacy with { ShowSensitiveContentInFullscreen = value }
            });
    }
    public bool ShowSensitiveContentWhenLocked
    {
        get => _showSensitiveContentWhenLocked;
        set => Set(value, ref _showSensitiveContentWhenLocked,
            settings => settings with
            {
                Privacy = settings.Privacy with { ShowSensitiveContentWhenLocked = value }
            });
    }

    public bool AutomaticUpdateChecksEnabled
    {
        get => _automaticUpdateChecksEnabled;
        set
        {
            if (!SetProperty(ref _automaticUpdateChecksEnabled, value) ||
                _synchronizing)
            {
                return;
            }

            _storeUpdates?.SetAutomaticChecksEnabled(value);
        }
    }

    public StoreUpdateStatus StoreUpdateStatus => _storeUpdateSnapshot.Status;
    public bool IsStoreUpdateChecking =>
        StoreUpdateStatus == StoreUpdateStatus.Checking;
    public bool IsStoreUpdateAvailable =>
        StoreUpdateStatus == StoreUpdateStatus.UpdateAvailable;
    public string StoreUpdateStatusMessage => StoreUpdateStatus switch
    {
        StoreUpdateStatus.Checking => _localization.Get("Update.Checking"),
        StoreUpdateStatus.UpToDate => _localization.Get("Update.UpToDate"),
        StoreUpdateStatus.UpdateAvailable => _localization.Get("Update.Available"),
        StoreUpdateStatus.Offline => _localization.Get("Update.Offline"),
        StoreUpdateStatus.Failed => _localization.Get("Update.Failed"),
        _ => _localization.Get("Update.StoreOnly")
    };
    public string StoreUpdateVersionText =>
        _storeUpdateSnapshot.AvailableVersion is { } available
            ? _localization.Get(
                "Update.VersionPair",
                _storeUpdateSnapshot.CurrentVersion,
                available)
            : $"MiaDock {_storeUpdateSnapshot.CurrentVersion}";

    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (!SetProperty(ref _language, value)) return;
            OnPropertyChanged(nameof(LanguageIndex));
            if (_synchronizing) return;
            _localization.SetLanguage(value);
            RebuildLocalizedOptions();
            _settingsService.Update(s => s with { General = s.General with { Language = value } });
        }
    }
    public IslandVisibilityMode VisibilityMode { get => _visibilityMode; set { Set(value, ref _visibilityMode, s => s with { General = s.General with { VisibilityMode = value } }); OnPropertyChanged(nameof(VisibilityModeIndex)); } }
    public IslandInteractionMode InteractionMode { get => _interactionMode; set { Set(value, ref _interactionMode, s => s with { General = s.General with { InteractionMode = value } }); OnPropertyChanged(nameof(InteractionModeIndex)); } }
    public IslandPositionSetting Position { get => _position; set { Set(value, ref _position, s => s with { General = s.General with { Position = value } }); OnPropertyChanged(nameof(PositionIndex)); } }
    public double PassiveModuleReturnSeconds
    {
        get => _passiveModuleReturnSeconds;
        set => Set(
            value,
            ref _passiveModuleReturnSeconds,
            settings => settings with
            {
                General = settings.General with { PassiveModuleReturnSeconds = value }
            });
    }
    public ClockHourFormat ClockHourFormat
    {
        get => _clockHourFormat;
        set
        {
            SetClock(value, ref _clockHourFormat, clock => clock with { HourFormat = value });
            OnPropertyChanged(nameof(ClockHourFormatIndex));
        }
    }
    public bool ShowClockSeconds
    {
        get => _showClockSeconds;
        set => SetClock(value, ref _showClockSeconds, clock => clock with { ShowSeconds = value });
    }
    public bool ShowClockDate
    {
        get => _showClockDate;
        set => SetClock(value, ref _showClockDate, clock => clock with { ShowDate = value });
    }
    public ClockDateFormat ClockDateFormat
    {
        get => _clockDateFormat;
        set
        {
            SetClock(value, ref _clockDateFormat, clock => clock with { DateFormat = value });
            OnPropertyChanged(nameof(ClockDateFormatIndex));
        }
    }
    public bool ShowClockWeekday
    {
        get => _showClockWeekday;
        set => SetClock(value, ref _showClockWeekday, clock => clock with { ShowWeekday = value });
    }
    public ThemeStyle Theme
    {
        get => _theme;
        set
        {
            if (!SetProperty(ref _theme, value)) return;
            OnPropertyChanged(nameof(ThemeIndex));
            OnPropertyChanged(nameof(ThemeDescription));
            OnPropertyChanged(nameof(IsBlurredGlassTheme));
            OnPropertyChanged(nameof(IsBackgroundColorEditable));
            if (_synchronizing) return;
            var appearance = AppearanceThemePresets.ApplyWhenSafe(
                _settingsService.Current.Appearance,
                value);
            ApplyAppearanceAndSave(appearance);
        }
    }
    public string ThemeDescription => Theme switch
    {
        ThemeStyle.AppleLike => _localization.Text(
            "Tam siyah, sade ve yüksek kontrastlı dock.",
            "Pure black, minimal, high-contrast dock."),
        ThemeStyle.Windows11Mica => _localization.Text(
            "Windows 11 ile uyumlu, yumuşak Mica yüzeyi.",
            "A soft Mica surface that matches Windows 11."),
        ThemeStyle.Windows11MicaAlt => _localization.Text(
            "Daha katmanlı ve belirgin Mica görünümü.",
            "A more layered and pronounced Mica appearance."),
        ThemeStyle.Windows11Acrylic => _localization.Text(
            "Koyu renk tonlu Windows Acrylic yüzeyi.",
            "A dark-tinted Windows Acrylic surface."),
        ThemeStyle.Windows11AcrylicThin => _localization.Text(
            "Daha hafif ve saydam Windows Acrylic yüzeyi.",
            "A lighter, more transparent Windows Acrylic surface."),
        ThemeStyle.BlurredGlass => _localization.Text(
            "Arkasındaki masaüstünü gösteren renksiz, saydam ve bulanık cam.",
            "Colorless transparent glass that reveals and blurs the desktop behind it."),
        ThemeStyle.CustomSolidColor => _localization.Text(
            "Seçtiğiniz arka plan ve vurgu renklerini kullanan düz yüzey.",
            "A solid surface using your selected background and accent colors."),
        _ => string.Empty
    };
    public bool IsBlurredGlassTheme => Theme == ThemeStyle.BlurredGlass;
    public bool IsBackgroundColorEditable => !IsBlurredGlassTheme;
    public double CollapsedWidth { get => _collapsedWidth; set => Set(value, ref _collapsedWidth, s => s with { Appearance = s.Appearance with { CollapsedWidth = value } }); }
    public double CollapsedHeight { get => _collapsedHeight; set => Set(value, ref _collapsedHeight, s => s with { Appearance = s.Appearance with { CollapsedHeight = value } }); }
    public double HoverWidth { get => _hoverWidth; set => Set(value, ref _hoverWidth, s => s with { Appearance = s.Appearance with { HoverWidth = value } }); }
    public double HoverHeight { get => _hoverHeight; set => Set(value, ref _hoverHeight, s => s with { Appearance = s.Appearance with { HoverHeight = value } }); }
    public double ExpandedWidth { get => _expandedWidth; set => Set(value, ref _expandedWidth, s => s with { Appearance = s.Appearance with { ExpandedWidth = value } }); }
    public double ExpandedHeight { get => _expandedHeight; set => Set(value, ref _expandedHeight, s => s with { Appearance = s.Appearance with { ExpandedHeight = value } }); }
    public double NotificationWidth { get => _notificationWidth; set => Set(value, ref _notificationWidth, s => s with { Appearance = s.Appearance with { NotificationWidth = value } }); }
    public double NotificationHeight { get => _notificationHeight; set => Set(value, ref _notificationHeight, s => s with { Appearance = s.Appearance with { NotificationHeight = value } }); }
    public double CornerRadius { get => _cornerRadius; set => Set(value, ref _cornerRadius, s => s with { Appearance = s.Appearance with { CornerRadius = value } }); }
    public string BackgroundColor { get => _backgroundColor; set => Set(value, ref _backgroundColor, s => s with { Appearance = s.Appearance with { BackgroundColor = value } }); }
    public string AccentColor { get => _accentColor; set => Set(value, ref _accentColor, s => s with { Appearance = s.Appearance with { AccentColor = value } }); }
    public double Opacity { get => _opacity; set => Set(value, ref _opacity, s => s with { Appearance = s.Appearance with { Opacity = value } }); }
    public double ShadowIntensity { get => _shadowIntensity; set => Set(value, ref _shadowIntensity, s => s with { Appearance = s.Appearance with { ShadowIntensity = value } }); }
    public double AnimationSpeed { get => _animationSpeed; set => Set(value, ref _animationSpeed, s => s with { Appearance = s.Appearance with { AnimationSpeed = value } }); }
    public IslandAnimationKind AnimationKind { get => _animationKind; set { Set(value, ref _animationKind, s => s with { Appearance = s.Appearance with { AnimationKind = value } }); OnPropertyChanged(nameof(AnimationKindIndex)); } }
    public string? SelectedSourceId { get => _selectedSourceId; set { Set(value, ref _selectedSourceId, s => s with { Media = s.Media with { SelectedSourceId = value } }); OnPropertyChanged(nameof(MediaSourceIndex)); } }
    public MediaFallbackSetting MediaFallback { get => _mediaFallback; set { Set(value, ref _mediaFallback, s => s with { Media = s.Media with { Fallback = value } }); OnPropertyChanged(nameof(MediaFallbackIndex)); } }
    public VolumeTargetSetting VolumeTarget { get => _volumeTarget; set { Set(value, ref _volumeTarget, s => s with { Media = s.Media with { VolumeTarget = value } }); OnPropertyChanged(nameof(VolumeTargetIndex)); } }
    public bool FullscreenEnabled { get => _fullscreenEnabled; set => Set(value, ref _fullscreenEnabled, s => s with { Fullscreen = s.Fullscreen with { Enabled = value } }); }
    public double FullscreenNotificationSeconds { get => _fullscreenNotificationSeconds; set => Set(value, ref _fullscreenNotificationSeconds, s => s with { Fullscreen = s.Fullscreen with { NotificationSeconds = value } }); }
    public FullscreenNotificationStyle FullscreenStyle { get => _fullscreenStyle; set { Set(value, ref _fullscreenStyle, s => s with { Fullscreen = s.Fullscreen with { Style = value } }); OnPropertyChanged(nameof(FullscreenStyleIndex)); } }
    public bool ShowTrackChanges { get => _showTrackChanges; set => Set(value, ref _showTrackChanges, s => s with { Fullscreen = s.Fullscreen with { ShowTrackChanges = value } }); }
    public MonitorSelectionMode MonitorMode { get => _monitorMode; set { Set(value, ref _monitorMode, s => s with { Monitor = s.Monitor with { Mode = value } }); OnPropertyChanged(nameof(MonitorModeIndex)); } }
    public string? FixedMonitorId { get => _fixedMonitorId; set { Set(value, ref _fixedMonitorId, s => s with { Monitor = s.Monitor with { FixedMonitorId = value } }); OnPropertyChanged(nameof(FixedMonitorIndex)); } }
    public bool ShowTrayIcon { get => _showTrayIcon; set => Set(value, ref _showTrayIcon, s => s with { Tray = s.Tray with { ShowIcon = value } }); }
    public bool ShowTrayMediaControls { get => _showTrayMediaControls; set => Set(value, ref _showTrayMediaControls, s => s with { Tray = s.Tray with { ShowMediaControls = value } }); }
    public bool TemporaryNotifications { get => _temporaryNotifications; set => Set(value, ref _temporaryNotifications, s => s with { Tray = s.Tray with { EnableTemporaryNotifications = value } }); }
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set
        {
            if (!SetProperty(ref _startWithWindows, value) || _synchronizing)
            {
                return;
            }

            _ = SetStartupEnabledAsync(value);
        }
    }
    public StartupLaunchMode LaunchMode { get => _launchMode; set { Set(value, ref _launchMode, s => s with { StartupShutdown = s.StartupShutdown with { LaunchMode = value } }); OnPropertyChanged(nameof(LaunchModeIndex)); } }
    public CloseBehaviorSetting CloseBehavior { get => _closeBehavior; set { Set(value, ref _closeBehavior, s => s with { StartupShutdown = s.StartupShutdown with { CloseBehavior = value, HasConfirmedCloseBehavior = true } }); OnPropertyChanged(nameof(CloseBehaviorIndex)); } }

    public int LanguageIndex { get => IndexOf(Languages, Language); set => Language = ValueAt(Languages, value, Language); }
    public int VisibilityModeIndex { get => IndexOf(VisibilityModes, VisibilityMode); set => VisibilityMode = ValueAt(VisibilityModes, value, VisibilityMode); }
    public int InteractionModeIndex { get => IndexOf(InteractionModes, InteractionMode); set => InteractionMode = ValueAt(InteractionModes, value, InteractionMode); }
    public int PositionIndex { get => IndexOf(Positions, Position); set => Position = ValueAt(Positions, value, Position); }
    public int ClockHourFormatIndex { get => IndexOf(ClockHourFormats, ClockHourFormat); set => ClockHourFormat = ValueAt(ClockHourFormats, value, ClockHourFormat); }
    public int ClockDateFormatIndex { get => IndexOf(ClockDateFormats, ClockDateFormat); set => ClockDateFormat = ValueAt(ClockDateFormats, value, ClockDateFormat); }
    public int ThemeIndex { get => IndexOf(Themes, Theme); set => Theme = ValueAt(Themes, value, Theme); }
    public int AnimationKindIndex { get => IndexOf(AnimationKinds, AnimationKind); set => AnimationKind = ValueAt(AnimationKinds, value, AnimationKind); }
    public int MediaFallbackIndex { get => IndexOf(MediaFallbackModes, MediaFallback); set => MediaFallback = ValueAt(MediaFallbackModes, value, MediaFallback); }
    public int VolumeTargetIndex { get => IndexOf(VolumeTargets, VolumeTarget); set => VolumeTarget = ValueAt(VolumeTargets, value, VolumeTarget); }
    public int FullscreenStyleIndex { get => IndexOf(FullscreenStyles, FullscreenStyle); set => FullscreenStyle = ValueAt(FullscreenStyles, value, FullscreenStyle); }
    public int MonitorModeIndex { get => IndexOf(MonitorModes, MonitorMode); set => MonitorMode = ValueAt(MonitorModes, value, MonitorMode); }
    public int LaunchModeIndex { get => IndexOf(LaunchModes, LaunchMode); set => LaunchMode = ValueAt(LaunchModes, value, LaunchMode); }
    public int CloseBehaviorIndex { get => IndexOf(CloseBehaviors, CloseBehavior); set => CloseBehavior = ValueAt(CloseBehaviors, value, CloseBehavior); }
    public int MediaSourceIndex
    {
        get => MediaSources.Select((source, index) => (source, index)).FirstOrDefault(item => item.source.Id == SelectedSourceId).index is var index &&
               MediaSources.ElementAtOrDefault(index)?.Id == SelectedSourceId ? index : -1;
        set => SelectedSourceId = value >= 0 && value < MediaSources.Count ? MediaSources[value].Id : null;
    }
    public int FixedMonitorIndex
    {
        get => Displays.Select((display, index) => (display, index)).FirstOrDefault(item => item.display.Id == FixedMonitorId).index is var index &&
               Displays.ElementAtOrDefault(index)?.Id == FixedMonitorId ? index : -1;
        set => FixedMonitorId = value >= 0 && value < Displays.Count ? Displays[value].Id : null;
    }

    public void Dispose()
    {
        _settingsService.SettingsChanged -= OnSettingsChanged;
        _music.PropertyChanged -= OnMusicPropertyChanged;
        if (_displayTopology is not null)
        {
            _displayTopology.DisplaysChanged -= OnDisplaysChanged;
        }
        if (_hotKeyService is not null)
        {
            _hotKeyService.RegistrationsChanged -= OnHotKeyRegistrationsChanged;
        }
        if (_notificationService is not null)
        {
            _notificationService.AccessStateChanged -= OnNotificationAccessStateChanged;
            _notificationService.SourcesChanged -= OnNotificationSourcesChanged;
        }
        if (_moduleCatalog is not null)
        {
            _moduleCatalog.Changed -= OnModuleCatalogChanged;
        }
        if (_storeUpdates is not null)
        {
            _storeUpdates.UpdateAvailabilityChanged -= OnStoreUpdateAvailabilityChanged;
        }
    }

    private void Set<T>(T value, ref T field, Func<MiaDockSettings, MiaDockSettings> update)
    {
        if (!SetProperty(ref field, value) || _synchronizing)
        {
            return;
        }

        _settingsService.Update(update);
    }

    private void SetClock<T>(
        T value,
        ref T field,
        Func<ClockDisplaySettings, ClockDisplaySettings> update)
    {
        Set(
            value,
            ref field,
            settings => settings with
            {
                General = settings.General with
                {
                    Clock = update(settings.General.Clock)
                }
            });
    }

    private static int IndexOf<T>(IReadOnlyList<SettingOption<T>> options, T value)
    {
        var comparer = EqualityComparer<T>.Default;
        for (var index = 0; index < options.Count; index++)
        {
            if (comparer.Equals(options[index].Value, value)) return index;
        }
        return -1;
    }

    private static T ValueAt<T>(IReadOnlyList<SettingOption<T>> options, int index, T fallback) =>
        index >= 0 && index < options.Count ? options[index].Value : fallback;

    private void RebuildLocalizedOptions()
    {
        string L(string turkish, string english) => _localization.Text(turkish, english);
        Languages = [new(AppLanguage.Turkish, "Türkçe"), new(AppLanguage.English, "English")];
        VisibilityModes =
        [
            new(IslandVisibilityMode.Always, L("Her zaman görünür", "Always visible")),
            new(IslandVisibilityMode.EventsOnly, L("Yalnızca olaylarda", "Events only"))
        ];
        InteractionModes =
        [
            new(IslandInteractionMode.Hover, L("Fare üzerine gelince", "On pointer hover")),
            new(IslandInteractionMode.Click, L("Tıklayınca", "On click")),
            new(IslandInteractionMode.HoverAndClick, L("Fare ve tıklama", "Hover and click"))
        ];
        Positions =
        [
            new(IslandPositionSetting.TopCenter, L("Üst orta", "Top center")),
            new(IslandPositionSetting.TopLeft, L("Üst sol", "Top left")),
            new(IslandPositionSetting.TopRight, L("Üst sağ", "Top right")),
            new(IslandPositionSetting.BottomCenter, L("Alt orta", "Bottom center")),
            new(IslandPositionSetting.BottomLeft, L("Alt sol", "Bottom left")),
            new(IslandPositionSetting.BottomRight, L("Alt sağ", "Bottom right"))
        ];
        ClockHourFormats =
        [
            new(ClockHourFormat.TwentyFourHour, L("24 saat", "24-hour")),
            new(ClockHourFormat.TwelveHour, L("12 saat", "12-hour"))
        ];
        ClockDateFormats =
        [
            new(ClockDateFormat.Short, L("Kısa", "Short")),
            new(ClockDateFormat.Long, L("Uzun", "Long"))
        ];
        Themes =
        [
            new(ThemeStyle.AppleLike, L("Apple benzeri", "Apple-like")),
            new(ThemeStyle.Windows11Mica, "Windows 11 Mica"),
            new(ThemeStyle.Windows11MicaAlt, "Windows 11 Mica Alt"),
            new(ThemeStyle.Windows11Acrylic, "Windows 11 Acrylic"),
            new(ThemeStyle.Windows11AcrylicThin, "Windows 11 Acrylic Thin"),
            new(ThemeStyle.BlurredGlass, L("Bulanık Cam", "Blurred Glass")),
            new(ThemeStyle.CustomSolidColor, L("Özel Düz Renk", "Custom solid color"))
        ];
        AnimationKinds =
        [
            new(IslandAnimationKind.Spring, L("Yay", "Spring")),
            new(IslandAnimationKind.ScaleFade, L("Ölçek ve solma", "Scale and fade")),
            new(IslandAnimationKind.SlideFade, L("Kayma ve solma", "Slide and fade"))
        ];
        MediaFallbackModes =
        [
            new(MediaFallbackSetting.SelectedOnly, L("Yalnızca seçili uygulama", "Selected app only")),
            new(MediaFallbackSetting.UseAnotherActiveSession, L("Seçili uygulama yoksa diğerine geç", "Use another active session"))
        ];
        VolumeTargets =
        [
            new(VolumeTargetSetting.SystemMaster, L("Windows ana sesi", "Windows master volume")),
            new(VolumeTargetSetting.SelectedApplication, L("Seçili uygulama sesi", "Selected app volume"))
        ];
        FullscreenStyles =
        [
            new(FullscreenNotificationStyle.Minimal, L("Sade", "Minimal")),
            new(FullscreenNotificationStyle.WithControls, L("Kontrollü", "With controls"))
        ];
        MonitorModes =
        [
            new(MonitorSelectionMode.Primary, L("Ana monitör", "Primary display")),
            new(MonitorSelectionMode.ActiveWindow, L("Aktif pencerenin monitörü", "Active window display")),
            new(MonitorSelectionMode.Fixed, L("Sabit monitör", "Fixed display"))
        ];
        LaunchModes =
        [
            new(StartupLaunchMode.Island, L("Doğrudan dock'u başlat", "Start the dock")),
            new(StartupLaunchMode.Settings, L("Ayarları aç", "Open Settings")),
            new(StartupLaunchMode.SilentTray, L("Sistem tepsisinde sessiz başlat", "Start silently in system tray"))
        ];
        CloseBehaviors =
        [
            new(CloseBehaviorSetting.MinimizeToTray, L("Sistem tepsisine küçült", "Minimize to system tray")),
            new(CloseBehaviorSetting.Exit, L("Uygulamadan çık", "Exit the app"))
        ];

        foreach (var propertyName in new[]
                 {
                     nameof(Languages), nameof(VisibilityModes), nameof(InteractionModes), nameof(Positions),
                     nameof(ClockHourFormats), nameof(ClockDateFormats),
                     nameof(Themes), nameof(AnimationKinds), nameof(MediaFallbackModes), nameof(VolumeTargets),
                     nameof(FullscreenStyles), nameof(MonitorModes), nameof(LaunchModes), nameof(CloseBehaviors),
                     nameof(LanguageIndex), nameof(VisibilityModeIndex), nameof(InteractionModeIndex),
                     nameof(PositionIndex), nameof(ClockHourFormatIndex), nameof(ClockDateFormatIndex),
                     nameof(ThemeIndex), nameof(AnimationKindIndex),
                     nameof(MediaFallbackIndex), nameof(VolumeTargetIndex), nameof(FullscreenStyleIndex),
                     nameof(MonitorModeIndex), nameof(LaunchModeIndex), nameof(CloseBehaviorIndex),
                     nameof(ThemeDescription), nameof(IsBlurredGlassTheme), nameof(IsBackgroundColorEditable),
                     nameof(StoreUpdateStatusMessage), nameof(StoreUpdateVersionText)
                 })
        {
            OnPropertyChanged(propertyName);
        }
        RefreshModuleItems(_settingsService.Current);
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args) => LoadFrom(args.Current);

    private void OnMusicPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MusicModuleViewModel.Sources))
        {
            OnPropertyChanged(nameof(MediaSources));
            OnPropertyChanged(nameof(MediaSourceIndex));
        }
    }

    private void OnDisplaysChanged(object? sender, IReadOnlyList<DisplayDescriptor> displays) =>
        NotifyDisplayOptionsChanged();

    private void NotifyDisplayOptionsChanged()
    {
        OnPropertyChanged(nameof(Displays));
        OnPropertyChanged(nameof(FixedMonitorIndex));
    }

    private void LoadFrom(MiaDockSettings settings)
    {
        _synchronizing = true;
        try
        {
            var languageChanged = _localization.CurrentLanguage != settings.General.Language;
            _localization.SetLanguage(settings.General.Language);
            if (languageChanged || Languages.Count == 0)
            {
                RebuildLocalizedOptions();
            }
            Language = settings.General.Language;
            VisibilityMode = settings.General.VisibilityMode;
            InteractionMode = settings.General.InteractionMode;
            Position = settings.General.Position;
            PassiveModuleReturnSeconds = settings.General.PassiveModuleReturnSeconds;
            ClockHourFormat = settings.General.Clock.HourFormat;
            ShowClockSeconds = settings.General.Clock.ShowSeconds;
            ShowClockDate = settings.General.Clock.ShowDate;
            ClockDateFormat = settings.General.Clock.DateFormat;
            ShowClockWeekday = settings.General.Clock.ShowWeekday;
            Theme = settings.Appearance.Theme;
            CollapsedWidth = settings.Appearance.CollapsedWidth;
            CollapsedHeight = settings.Appearance.CollapsedHeight;
            HoverWidth = settings.Appearance.HoverWidth;
            HoverHeight = settings.Appearance.HoverHeight;
            ExpandedWidth = settings.Appearance.ExpandedWidth;
            ExpandedHeight = settings.Appearance.ExpandedHeight;
            NotificationWidth = settings.Appearance.NotificationWidth;
            NotificationHeight = settings.Appearance.NotificationHeight;
            CornerRadius = settings.Appearance.CornerRadius;
            BackgroundColor = settings.Appearance.BackgroundColor;
            AccentColor = settings.Appearance.AccentColor;
            Opacity = settings.Appearance.Opacity;
            ShadowIntensity = settings.Appearance.ShadowIntensity;
            AnimationSpeed = settings.Appearance.AnimationSpeed;
            AnimationKind = settings.Appearance.AnimationKind;
            SelectedSourceId = settings.Media.SelectedSourceId;
            MediaFallback = settings.Media.Fallback;
            VolumeTarget = settings.Media.VolumeTarget;
            FullscreenEnabled = settings.Fullscreen.Enabled;
            FullscreenNotificationSeconds = settings.Fullscreen.NotificationSeconds;
            FullscreenStyle = settings.Fullscreen.Style;
            ShowTrackChanges = settings.Fullscreen.ShowTrackChanges;
            MonitorMode = settings.Monitor.Mode;
            FixedMonitorId = settings.Monitor.FixedMonitorId;
            ShowTrayIcon = settings.Tray.ShowIcon;
            ShowTrayMediaControls = settings.Tray.ShowMediaControls;
            TemporaryNotifications = settings.Tray.EnableTemporaryNotifications;
            StartWithWindows = settings.StartupShutdown.StartWithWindows;
            LaunchMode = settings.StartupShutdown.LaunchMode;
            CloseBehavior = settings.StartupShutdown.CloseBehavior;
            var batteryOptions = BatteryModuleOptions.FromEnvelope(
                settings.Modules.TryGetValue("battery", out var batteryEnvelope) ? batteryEnvelope : null);
            BatteryLowThreshold = batteryOptions.LowThresholdPercent;
            BatteryCriticalThreshold = batteryOptions.CriticalThresholdPercent;
            BatteryEmergencyThreshold = batteryOptions.EmergencyThresholdPercent;
            HotKeysEnabled = settings.HotKeys.IsEnabled;
            ToggleDockHotKey = GetHotKey(settings, HotKeyAction.ToggleDock);
            ToggleExpandedHotKey = GetHotKey(settings, HotKeyAction.ToggleExpanded);
            NextModuleHotKey = GetHotKey(settings, HotKeyAction.NextModule);
            MediaPlayPauseHotKey = GetHotKey(settings, HotKeyAction.MediaPlayPause);
            TimerPauseResumeHotKey = GetHotKey(settings, HotKeyAction.TimerPauseResume);
            UpdateHotKeyStatus();
            var notificationOptions = NotificationModuleOptions.FromEnvelope(
                settings.Modules.TryGetValue("notifications", out var notificationEnvelope) ? notificationEnvelope : null);
            NotificationsEnabled = notificationOptions.IsEnabled;
            NotificationEventSeconds = notificationOptions.EventDuration.TotalSeconds;
            NotificationsInFullscreen = notificationOptions.ShowInFullscreen;
            NotificationUseAllowList = notificationOptions.UseAllowList;
            NotificationAccessState = _notificationService?.AccessState ?? NotificationAccessState.Unsupported;
            LoadNotificationApplications(notificationOptions);
            ShowSensitiveContentInFullscreen = settings.Privacy.ShowSensitiveContentInFullscreen;
            ShowSensitiveContentWhenLocked = settings.Privacy.ShowSensitiveContentWhenLocked;
            AutomaticUpdateChecksEnabled =
                settings.StoreUpdates.AutomaticChecksEnabled;
            RefreshModuleItems(settings);
        }
        finally
        {
            _synchronizing = false;
        }
    }

    private void ApplyAppearanceAndSave(AppearanceSettings appearance)
    {
        _synchronizing = true;
        try
        {
            Theme = appearance.Theme;
            CollapsedWidth = appearance.CollapsedWidth;
            CollapsedHeight = appearance.CollapsedHeight;
            HoverWidth = appearance.HoverWidth;
            HoverHeight = appearance.HoverHeight;
            ExpandedWidth = appearance.ExpandedWidth;
            ExpandedHeight = appearance.ExpandedHeight;
            NotificationWidth = appearance.NotificationWidth;
            NotificationHeight = appearance.NotificationHeight;
            CornerRadius = appearance.CornerRadius;
            BackgroundColor = appearance.BackgroundColor;
            AccentColor = appearance.AccentColor;
            Opacity = appearance.Opacity;
            ShadowIntensity = appearance.ShadowIntensity;
            AnimationSpeed = appearance.AnimationSpeed;
            AnimationKind = appearance.AnimationKind;
        }
        finally
        {
            _synchronizing = false;
        }

        _settingsService.Update(settings => settings with { Appearance = appearance });
        if (_uiDispatcher is null || !_uiDispatcher.TryEnqueue(RefreshAppearanceBindings))
        {
            RefreshAppearanceBindings();
        }
    }

    private void RefreshAppearanceBindings()
    {
        foreach (var propertyName in new[]
                 {
                     nameof(Theme), nameof(ThemeIndex), nameof(CollapsedWidth), nameof(CollapsedHeight),
                     nameof(HoverWidth), nameof(HoverHeight), nameof(ExpandedWidth), nameof(ExpandedHeight),
                     nameof(NotificationWidth), nameof(NotificationHeight), nameof(CornerRadius),
                     nameof(BackgroundColor), nameof(AccentColor), nameof(Opacity), nameof(ShadowIntensity),
                     nameof(AnimationSpeed), nameof(AnimationKind), nameof(AnimationKindIndex),
                     nameof(ThemeDescription), nameof(IsBlurredGlassTheme), nameof(IsBackgroundColorEditable)
                 })
        {
            OnPropertyChanged(propertyName);
        }
    }

    private void SetBatteryThreshold(double value, ref double field, ThresholdKind kind)
    {
        if (!SetProperty(ref field, value) || _synchronizing) return;
        var low = kind == ThresholdKind.Low ? (int)Math.Round(value) : (int)Math.Round(BatteryLowThreshold);
        var critical = kind == ThresholdKind.Critical ? (int)Math.Round(value) : (int)Math.Round(BatteryCriticalThreshold);
        var emergency = kind == ThresholdKind.Emergency ? (int)Math.Round(value) : (int)Math.Round(BatteryEmergencyThreshold);
        _settingsService.Update(settings =>
        {
            var modules = new Dictionary<string, ModuleSettingsEnvelope>(settings.Modules, StringComparer.Ordinal);
            var envelope = modules.TryGetValue("battery", out var current)
                ? current
                : ModuleSettingsEnvelope.BatteryDefault;
            modules["battery"] = BatteryModuleOptions.ApplyThresholds(envelope, low, critical, emergency);
            return settings with { Modules = modules };
        });
    }

    private void SetHotKey(
        HotKeyAction action,
        HotKeyGestureSetting? value,
        ref HotKeyGestureSetting? field)
    {
        if (!SetProperty(ref field, value) || _synchronizing) return;
        _settingsService.Update(settings =>
        {
            var bindings = new Dictionary<HotKeyAction, HotKeyGestureSetting>(settings.HotKeys.Bindings);
            if (value is null) bindings.Remove(action);
            else bindings[action] = value;
            return settings with { HotKeys = settings.HotKeys with { Bindings = bindings } };
        });
    }

    public async Task<bool> SetNotificationsEnabledAsync(bool enabled)
    {
        if (!enabled)
        {
            UpdateNotificationOptions(options => options with { IsEnabled = false });
            return true;
        }

        if (_notificationService is null) return false;
        var state = _notificationService.AccessState;
        if (state != NotificationAccessState.Allowed)
        {
            state = await _notificationService.RequestAccessAsync();
        }
        NotificationAccessState = state;
        if (state != NotificationAccessState.Allowed) return false;
        UpdateNotificationOptions(options => options with { IsEnabled = true });
        return true;
    }

    public async Task<bool> SetModuleEnabledAsync(string moduleId, bool enabled)
    {
        if (moduleId == "notifications")
        {
            var result = await SetNotificationsEnabledAsync(enabled);
            RefreshModuleItems(_settingsService.Current);
            return result;
        }

        UpdateModuleEnvelope(moduleId, envelope => envelope with { IsEnabled = enabled });
        return true;
    }

    private void BuildModuleItems()
    {
        foreach (var module in ModuleDefinitions())
        {
            ModuleItems.Add(new ModuleSettingsItemViewModel(
                module.Id,
                module.TurkishTitle,
                module.TurkishDescription,
                module.IconGlyph,
                OnModuleItemChanged));
        }
    }

    private void RefreshModuleItems(MiaDockSettings settings)
    {
        if (ModuleItems.Count == 0) return;
        string L(string turkish, string english) => _localization.Text(turkish, english);
        foreach (var definition in ModuleDefinitions())
        {
            var item = ModuleItems.First(candidate => candidate.ModuleId == definition.Id);
            var envelope = settings.Modules.TryGetValue(definition.Id, out var value)
                ? value
                : DefaultEnvelope(definition.Id);
            var availability = _moduleCatalog?.GetAvailability(definition.Id, envelope.IsEnabled)
                ?? new ModuleAvailability(
                    envelope.IsEnabled ? ModuleAvailabilityState.Ready : ModuleAvailabilityState.Disabled);
            item.Refresh(
                L(definition.TurkishTitle, definition.EnglishTitle),
                L(definition.TurkishDescription, definition.EnglishDescription),
                envelope.IsEnabled,
                envelope.EventDurationSeconds,
                envelope.ShowInFullscreen,
                availability,
                AvailabilityText(availability.State));
        }
        OnPropertyChanged(nameof(EnabledModuleCount));
        OnPropertyChanged(nameof(EnabledModuleSummary));
    }

    private string AvailabilityText(ModuleAvailabilityState state) => state switch
    {
        ModuleAvailabilityState.Ready => _localization.Text("Hazır", "Ready"),
        ModuleAvailabilityState.Disabled => _localization.Text("Kapalı", "Disabled"),
        ModuleAvailabilityState.PermissionRequired => _localization.Text("İzin gerekli", "Permission required"),
        ModuleAvailabilityState.PermissionDenied => _localization.Text("İzin reddedildi", "Permission denied"),
        ModuleAvailabilityState.ApiUnavailable => _localization.Text("Windows API kullanılamıyor", "Windows API unavailable"),
        ModuleAvailabilityState.NoCompatibleDevice => _localization.Text("Uyumlu cihaz bulunamadı", "No compatible device"),
        _ => _localization.Text("Geçici hata", "Temporary error")
    };

    private void OnModuleItemChanged(ModuleSettingsItemViewModel item)
    {
        if (_synchronizing) return;
        UpdateModuleEnvelope(item.ModuleId, envelope => envelope with
        {
            EventDurationSeconds = item.EventDurationSeconds,
            ShowInFullscreen = item.ShowInFullscreen
        });
    }

    private void UpdateModuleEnvelope(
        string moduleId,
        Func<ModuleSettingsEnvelope, ModuleSettingsEnvelope> update)
    {
        _settingsService.Update(settings =>
        {
            var modules = new Dictionary<string, ModuleSettingsEnvelope>(settings.Modules, StringComparer.Ordinal);
            var current = modules.TryGetValue(moduleId, out var envelope)
                ? envelope
                : DefaultEnvelope(moduleId);
            modules[moduleId] = update(current);
            return settings with { Modules = modules };
        });
    }

    private static ModuleSettingsEnvelope DefaultEnvelope(string moduleId) => moduleId switch
    {
        "media" => ModuleSettingsEnvelope.MediaDefault,
        "system-activity" => ModuleSettingsEnvelope.SystemActivityDefault,
        "battery" => ModuleSettingsEnvelope.BatteryDefault,
        "network" => ModuleSettingsEnvelope.NetworkDefault,
        "bluetooth" => ModuleSettingsEnvelope.BluetoothDefault,
        "timer" => ModuleSettingsEnvelope.TimerDefault,
        "notifications" => ModuleSettingsEnvelope.NotificationsDefault,
        "transfers" => ModuleSettingsEnvelope.TransfersDefault,
        _ => ModuleSettingsEnvelope.MediaDefault
    };

    private static IReadOnlyList<ModuleDefinition> ModuleDefinitions() =>
    [
        new("media", "Medya", "Media", "Windows medya oturumları ve oynatma denetimleri.", "Windows media sessions and playback controls.", "\uE8D6"),
        new("system-activity", "Ses, mikrofon, kamera ve arama", "Audio, microphone, camera and calls", "Ses durumunu ve gizlilik göstergelerini yerel olarak izler.", "Locally monitors audio state and privacy indicators.", "\uE767"),
        new("battery", "Pil", "Battery", "Şarj, enerji tasarrufu ve pil eşiklerini gösterir.", "Shows charging, energy saver and battery thresholds.", "\uE850"),
        new("network", "Ağ", "Network", "Bağlantı türünü ve görünürken aktarım hızını gösterir.", "Shows connection type and throughput while visible.", "\uE968"),
        new("bluetooth", "Bluetooth", "Bluetooth", "Eşleştirilmiş cihaz bağlantı durumlarını izler.", "Monitors paired device connection states.", "\uE702"),
        new("timer", "Zamanlayıcı ve kronometre", "Timer and stopwatch", "Geri sayım ve kronometre araçlarını dock'a ekler.", "Adds countdown and stopwatch tools to the dock.", "\uE823"),
        new("notifications", "Windows bildirimleri", "Windows notifications", "İzin verdiğiniz uygulama başlıklarını geçici gösterir.", "Temporarily shows titles from apps you allow.", "\uEA8F"),
        new("transfers", "Dosya aktarımları", "File transfers", "Yerel sağlayıcıların bildirdiği aktarım ilerlemesini gösterir.", "Shows transfer progress reported by local providers.", "\uE898")
    ];

    private sealed record ModuleDefinition(
        string Id,
        string TurkishTitle,
        string EnglishTitle,
        string TurkishDescription,
        string EnglishDescription,
        string IconGlyph);

    private void SetNotificationOptions<T>(
        T value,
        ref T field,
        Func<NotificationModuleOptions, NotificationModuleOptions> update)
    {
        if (!SetProperty(ref field, value) || _synchronizing) return;
        UpdateNotificationOptions(update);
    }

    private void UpdateNotificationOptions(Func<NotificationModuleOptions, NotificationModuleOptions> update)
    {
        _settingsService.Update(settings =>
        {
            var modules = new Dictionary<string, ModuleSettingsEnvelope>(settings.Modules, StringComparer.Ordinal);
            var current = NotificationModuleOptions.FromEnvelope(
                modules.TryGetValue("notifications", out var envelope) ? envelope : null);
            modules["notifications"] = NotificationModuleOptions.ToEnvelope(update(current));
            return settings with { Modules = modules };
        });
    }

    private void LoadNotificationApplications(NotificationModuleOptions options)
    {
        NotificationApplications.Clear();
        foreach (var source in _notificationService?.Sources ?? Array.Empty<NotificationSourceInfo>())
        {
            NotificationApplications.Add(new NotificationApplicationSettingItem(
                source.Id,
                source.DisplayName,
                options.IsApplicationAllowed(source.Id),
                options.CanShowBody(source.Id),
                OnNotificationApplicationChanged));
        }
        OnPropertyChanged(nameof(NotificationApplications));
    }

    private void OnNotificationApplicationChanged(NotificationApplicationSettingItem item)
    {
        UpdateNotificationOptions(options =>
        {
            var allowed = options.AllowedApplications.ToHashSet(StringComparer.Ordinal);
            var blocked = options.BlockedApplications.ToHashSet(StringComparer.Ordinal);
            var body = options.BodyAllowedApplications.ToHashSet(StringComparer.Ordinal);
            if (item.IsVisible)
            {
                allowed.Add(item.Id);
                blocked.Remove(item.Id);
            }
            else
            {
                allowed.Remove(item.Id);
                blocked.Add(item.Id);
            }
            if (item.ShowBody) body.Add(item.Id); else body.Remove(item.Id);
            return options with
            {
                AllowedApplications = allowed,
                BlockedApplications = blocked,
                BodyAllowedApplications = body
            };
        });
    }

    private void OnNotificationAccessStateChanged(object? sender, NotificationAccessState state) =>
        DispatchNotificationUpdate(() =>
        {
            NotificationAccessState = state;
            RefreshModuleItems(_settingsService.Current);
        });

    private void OnNotificationSourcesChanged(object? sender, IReadOnlyList<NotificationSourceInfo> sources) =>
        DispatchNotificationUpdate(() => LoadNotificationApplications(NotificationModuleOptions.FromEnvelope(
            _settingsService.Current.Modules.TryGetValue("notifications", out var envelope) ? envelope : null)));

    private void DispatchNotificationUpdate(Action update)
    {
        if (_uiDispatcher is null || _uiDispatcher.HasThreadAccess) update();
        else _uiDispatcher.TryEnqueue(update);
    }

    private void OnModuleCatalogChanged(object? sender, EventArgs args) =>
        DispatchNotificationUpdate(() => RefreshModuleItems(_settingsService.Current));

    private void OnStoreUpdateAvailabilityChanged(
        object? sender,
        StoreUpdateSnapshot snapshot) =>
        DispatchNotificationUpdate(() => ApplyStoreUpdateSnapshot(snapshot));

    private void ApplyStoreUpdateSnapshot(StoreUpdateSnapshot snapshot)
    {
        _storeUpdateSnapshot = snapshot;
        foreach (var propertyName in new[]
                 {
                     nameof(StoreUpdateStatus),
                     nameof(IsStoreUpdateChecking),
                     nameof(IsStoreUpdateAvailable),
                     nameof(StoreUpdateStatusMessage),
                     nameof(StoreUpdateVersionText)
                 })
        {
            OnPropertyChanged(propertyName);
        }
        CheckForUpdatesCommand.NotifyCanExecuteChanged();
        OpenStoreCommand.NotifyCanExecuteChanged();
    }

    private async Task CheckForUpdatesAsync()
    {
        if (_storeUpdates is not null)
        {
            await _storeUpdates.CheckNowAsync();
        }
    }

    private async Task OpenStoreAsync()
    {
        if (_storeUpdates is not null)
        {
            await _storeUpdates.OpenStorePageAsync();
        }
    }

    private void UpdateNotificationStatus()
    {
        NotificationStatusMessage = NotificationAccessState switch
        {
            NotificationAccessState.Allowed => _localization.Text("Windows bildirim erişimine izin verdi.", "Windows notification access is allowed."),
            NotificationAccessState.Denied => _localization.Text("Erişim reddedildi. Windows Ayarları'ndan bildirim erişimini etkinleştirin.", "Access was denied. Enable notification access in Windows Settings."),
            NotificationAccessState.PackageIdentityRequired => _localization.Text("Bildirim dinleyicisi geliştirme MSIX paketiyle çalışır.", "The notification listener requires the development MSIX package."),
            NotificationAccessState.Unsupported => _localization.Text("Bu Windows sürümünde bildirim dinleyicisi kullanılamıyor.", "The notification listener is unavailable on this Windows version."),
            NotificationAccessState.Faulted => _localization.Text("Bildirim erişimi başlatılamadı.", "Notification access could not be initialized."),
            _ => _localization.Text("Bildirim modülünü açtığınızda Windows izni istenecek.", "Windows permission will be requested when you enable the notification module.")
        };
        OnPropertyChanged(nameof(CanEnableNotifications));
    }

    private static HotKeyGestureSetting? GetHotKey(MiaDockSettings settings, HotKeyAction action) =>
        settings.HotKeys.Bindings.TryGetValue(action, out var gesture) ? gesture : null;

    private void OnHotKeyRegistrationsChanged(object? sender, EventArgs args) => UpdateHotKeyStatus();

    private void UpdateHotKeyStatus()
    {
        if (!HotKeysEnabled)
        {
            HotKeyStatusMessage = _localization.Text("Global kısayollar kapalı.", "Global shortcuts are disabled.");
            return;
        }

        var statuses = _hotKeyService?.RegistrationStatuses;
        if (statuses?.Values.Any(value => value == HotKeyRegistrationStatus.Conflict) == true)
        {
            HotKeyStatusMessage = _localization.Text("En az bir kısayol başka bir uygulama tarafından kullanılıyor.", "At least one shortcut is already used by another app.");
        }
        else if (_settingsService.Current.HotKeys.Bindings.Count == 0)
        {
            HotKeyStatusMessage = _localization.Text("Kullanmak istediğiniz eylemlere tuş atayın.", "Assign keys to the actions you want to use.");
        }
        else
        {
            HotKeyStatusMessage = _localization.Text("Atanmış global kısayollar etkin.", "Assigned global shortcuts are active.");
        }
    }

    private enum ThresholdKind { Low, Critical, Emergency }

    private async Task RefreshStartupStatusAsync()
    {
        if (_startupTaskService is null)
        {
            IsStartupTaskAvailable = false;
            StartupStatusMessage = _localization.Text("Başlangıç görevi bu çalıştırmada kullanılamıyor.", "The startup task is unavailable in this run.");
            return;
        }

        var status = await _startupTaskService.GetStatusAsync();
        ApplyStartupStatus(status, updateSetting: true);
    }

    private async Task SetStartupEnabledAsync(bool enabled)
    {
        if (_startupTaskService is null)
        {
            ApplyStartupStatus(StartupTaskStatus.Unavailable, updateSetting: true);
            return;
        }

        var status = await _startupTaskService.SetEnabledAsync(enabled);
        ApplyStartupStatus(status, updateSetting: true);
    }

    private void ApplyStartupStatus(StartupTaskStatus status, bool updateSetting)
    {
        IsStartupTaskAvailable = status != StartupTaskStatus.Unavailable;
        var enabled = status is StartupTaskStatus.Enabled or StartupTaskStatus.EnabledByPolicy;
        _synchronizing = true;
        try
        {
            StartWithWindows = enabled;
        }
        finally
        {
            _synchronizing = false;
        }

        StartupStatusMessage = status switch
        {
            StartupTaskStatus.Unavailable => _localization.Text("Bu seçenek, MSIX paketi kurulduğunda kullanılabilir.", "This option is available when the MSIX package is installed."),
            StartupTaskStatus.Failed => _localization.Text("Başlangıç ayarı değiştirilemedi. Windows Başlangıç Uygulamaları ayarını kontrol edin.", "The startup setting could not be changed. Check Windows Startup Apps settings."),
            StartupTaskStatus.DisabledByUser => _localization.Text("Windows bu görevi devre dışı bıraktı. Başlangıç Uygulamaları ayarından yeniden etkinleştirin.", "Windows disabled this task. Re-enable it from Startup Apps settings."),
            StartupTaskStatus.DisabledByPolicy => _localization.Text("Başlangıç görevi sistem ilkesi tarafından engelleniyor.", "The startup task is blocked by system policy."),
            StartupTaskStatus.EnabledByPolicy => _localization.Text("Başlangıç görevi sistem ilkesi tarafından etkinleştirildi.", "The startup task is enabled by system policy."),
            StartupTaskStatus.Enabled => _localization.Text("MiaDock Windows oturumu açıldığında başlayacak.", "MiaDock will start when you sign in to Windows."),
            _ => _localization.Text("MiaDock Windows ile başlamayacak.", "MiaDock will not start with Windows.")
        };

        if (updateSetting && _settingsService.Current.StartupShutdown.StartWithWindows != enabled)
        {
            _settingsService.Update(settings => settings with
            {
                StartupShutdown = settings.StartupShutdown with { StartWithWindows = enabled }
            });
        }
    }
}
