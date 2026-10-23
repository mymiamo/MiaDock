namespace MiaDock.Core.Settings;

public sealed record MiaDockSettings(
    int SchemaVersion,
    GeneralSettings General,
    AppearanceSettings Appearance,
    MediaSettings Media,
    FullscreenSettings Fullscreen,
    MonitorSettings Monitor,
    TraySettings Tray,
    StartupShutdownSettings StartupShutdown,
    OnboardingSettings Onboarding,
    GlobalHotKeySettings HotKeys,
    PresentationPrivacySettings Privacy,
    StoreUpdateSettings StoreUpdates,
    FocusSettings Focus,
    AudibleNotificationSettings AudibleNotifications,
    IReadOnlyDictionary<string, ModuleSettingsEnvelope> Modules,
    DockNavigationSettings? Navigation = null,
    IReadOnlyList<MonitorAppearanceProfile>? DisplayProfiles = null)
{
    public const int CurrentSchemaVersion = 34;

    public DockNavigationSettings EffectiveNavigation => Navigation ?? DockNavigationSettings.Default;

    public static MiaDockSettings Default { get; } = new(
        CurrentSchemaVersion,
        GeneralSettings.Default,
        AppearanceSettings.Default,
        MediaSettings.Default,
        FullscreenSettings.Default,
        MonitorSettings.Default,
        TraySettings.Default,
        StartupShutdownSettings.Default,
        OnboardingSettings.Default,
        GlobalHotKeySettings.Default,
        PresentationPrivacySettings.Default,
        StoreUpdateSettings.Default,
        FocusSettings.Default,
        AudibleNotificationSettings.Default,
        new Dictionary<string, ModuleSettingsEnvelope>(StringComparer.Ordinal)
        {
            ["media"] = ModuleSettingsEnvelope.MediaDefault,
            ["privacy"] = ModuleSettingsEnvelope.PrivacyDefault,
            ["system-activity"] = ModuleSettingsEnvelope.SystemActivityDefault,
            ["volume"] = ModuleSettingsEnvelope.VolumeDefault,
            ["battery"] = ModuleSettingsEnvelope.BatteryDefault,
            ["network"] = ModuleSettingsEnvelope.NetworkDefault,
            ["bluetooth"] = ModuleSettingsEnvelope.BluetoothDefault,
            ["device-hub"] = ModuleSettingsEnvelope.DeviceHubDefault,
            ["clipboard-peek"] = ModuleSettingsEnvelope.ClipboardPeekDefault,
            ["timer"] = ModuleSettingsEnvelope.TimerDefault,
            ["hourly-notification"] = ModuleSettingsEnvelope.HourlyNotificationDefault,
            ["notifications"] = ModuleSettingsEnvelope.NotificationsDefault,
            ["transfers"] = ModuleSettingsEnvelope.TransfersDefault
        },
        DockNavigationSettings.Default,
        Array.Empty<MonitorAppearanceProfile>());
}
