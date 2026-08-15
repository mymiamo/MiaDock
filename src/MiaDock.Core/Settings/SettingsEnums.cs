namespace MiaDock.Core.Settings;

public enum AppLanguage
{
    Turkish,
    English,
    Azerbaijani,
    SpanishSpain,
    SpanishMexico,
    PortugueseBrazil
}
public enum IslandVisibilityMode
{
    Always,
    EventsOnly,
    EdgeReveal
}
public enum IslandInteractionMode { Hover, Click, HoverAndClick }
public enum IslandPositionSetting
{
    TopCenter,
    TopLeft,
    TopRight,
    BottomCenter,
    BottomLeft,
    BottomRight,
    LeftCenter,
    RightCenter
}
public enum MediaFallbackSetting { SelectedOnly, UseAnotherActiveSession }
public enum VolumeTargetSetting { SystemMaster, SelectedApplication }
public enum FullscreenNotificationStyle { Minimal, WithControls }
public enum FullscreenDockBehavior
{
    HideCompletely,
    NotificationsOnly,
    EdgeReveal,
    KeepVisible
}
public enum MonitorSelectionMode { Primary, ActiveWindow, Fixed }
public enum StartupLaunchMode { Island, Settings, SilentTray }
public enum CloseBehaviorSetting { MinimizeToTray, Exit }
public enum TrayPrimaryAction { OpenSettings, ToggleDock }
