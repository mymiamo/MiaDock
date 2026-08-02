using MiaDock.Core.Modules;

namespace MiaDock.Core.Focus;

public sealed record FocusPolicySnapshot(
    bool IsActive,
    string? ProfileId,
    FocusDockVisibility DockVisibility,
    IReadOnlySet<string> AllowedModuleIds,
    ModuleEventPriority MinimumEventPriority,
    bool AllowFullscreenNotifications,
    bool AllowSensitiveContentInFullscreen,
    bool AllowSensitiveContentWhenLocked)
{
    private static readonly IReadOnlySet<string> NoModuleRestrictions =
        new HashSet<string>(StringComparer.Ordinal);

    public static FocusPolicySnapshot Inactive { get; } = new(
        false,
        null,
        FocusDockVisibility.UseGlobalSetting,
        NoModuleRestrictions,
        ModuleEventPriority.Low,
        true,
        true,
        true);

    public bool AllowsModule(string moduleId) =>
        !IsActive ||
        AllowedModuleIds.Count == 0 ||
        AllowedModuleIds.Contains(moduleId);

    public bool AllowsEvent(ModuleEvent moduleEvent) =>
        AllowsModule(moduleEvent.ModuleId) &&
        (!IsActive || moduleEvent.Priority >= MinimumEventPriority);

    public bool AllowsNormalDock(bool globalAlwaysVisible) =>
        !IsActive || DockVisibility == FocusDockVisibility.UseGlobalSetting
            ? globalAlwaysVisible
            : DockVisibility == FocusDockVisibility.AlwaysVisible;

    public bool AllowsTemporaryDock(bool isFullscreen) =>
        (!IsActive || DockVisibility != FocusDockVisibility.Hidden) &&
        (!isFullscreen || AllowFullscreenNotifications);
}
