using MiaDock.Core.Settings;
using MiaDock.Core.Theming;

namespace MiaDock.App.Models;

public sealed record OnboardingDraft(
    bool StartWithWindows,
    ThemeStyle Theme,
    string? SelectedSourceId,
    MonitorSelectionMode MonitorMode,
    string? FixedMonitorId,
    IslandPositionSetting Position,
    IslandInteractionMode InteractionMode,
    bool FullscreenEnabled,
    FullscreenNotificationStyle FullscreenStyle)
{
    public static OnboardingDraft FromSettings(MiaDockSettings settings) => new(
        settings.StartupShutdown.StartWithWindows,
        settings.Appearance.Theme,
        settings.Media.SelectedSourceId,
        settings.Monitor.Mode,
        settings.Monitor.FixedMonitorId,
        settings.General.Position,
        settings.General.InteractionMode,
        settings.Fullscreen.Enabled,
        settings.Fullscreen.Style);
}
