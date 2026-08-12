namespace MiaDock.Core.Settings;

public sealed record GeneralSettings(
    AppLanguage Language,
    IslandVisibilityMode VisibilityMode,
    IslandInteractionMode InteractionMode,
    IslandPositionSetting Position,
    double PassiveModuleReturnSeconds,
    ClockDisplaySettings Clock,
    bool ShowKeyboardLockEvents = true,
    bool ShowUsbDeviceEvents = true)
{
    public static GeneralSettings Default { get; } = new(
        AppLanguage.Turkish,
        IslandVisibilityMode.Always,
        IslandInteractionMode.HoverAndClick,
        IslandPositionSetting.TopCenter,
        8,
        ClockDisplaySettings.Default,
        true,
        true);
}
