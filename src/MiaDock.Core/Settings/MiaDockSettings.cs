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
    IReadOnlyDictionary<string, ModuleSettingsEnvelope> Modules)
{
    public const int CurrentSchemaVersion = 14;

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
        new Dictionary<string, ModuleSettingsEnvelope>(StringComparer.Ordinal)
        {
            ["media"] = ModuleSettingsEnvelope.MediaDefault,
            ["system-activity"] = ModuleSettingsEnvelope.SystemActivityDefault,
            ["battery"] = ModuleSettingsEnvelope.BatteryDefault,
            ["network"] = ModuleSettingsEnvelope.NetworkDefault,
            ["bluetooth"] = ModuleSettingsEnvelope.BluetoothDefault,
            ["timer"] = ModuleSettingsEnvelope.TimerDefault,
            ["notifications"] = ModuleSettingsEnvelope.NotificationsDefault,
            ["transfers"] = ModuleSettingsEnvelope.TransfersDefault
        });
}
