using MiaDock.Core.Modules;

namespace MiaDock.Core.Focus;

public sealed record FocusProfileBehavior(
    FocusDockVisibility DockVisibility,
    IReadOnlyList<string> AllowedModuleIds,
    ModuleEventPriority MinimumEventPriority,
    bool AllowFullscreenNotifications,
    bool AllowSensitiveContentInFullscreen,
    bool AllowSensitiveContentWhenLocked)
{
    public static FocusProfileBehavior Default { get; } = new(
        FocusDockVisibility.UseGlobalSetting,
        Array.Empty<string>(),
        ModuleEventPriority.Low,
        true,
        true,
        true);

    public bool CanShowSensitiveContentInFullscreen(bool globalPermission) =>
        globalPermission && AllowSensitiveContentInFullscreen;

    public bool CanShowSensitiveContentWhenLocked(bool globalPermission) =>
        globalPermission && AllowSensitiveContentWhenLocked;
}
