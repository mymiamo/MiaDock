namespace MiaDock.Core.Settings;

public enum AppLanguage { Turkish, English }
public enum IslandVisibilityMode { Always, EventsOnly }
public enum IslandInteractionMode { Hover, Click, HoverAndClick }
public enum IslandPositionSetting { TopCenter, TopLeft, TopRight, BottomCenter, BottomLeft, BottomRight }
public enum MediaFallbackSetting { SelectedOnly, UseAnotherActiveSession }
public enum VolumeTargetSetting { SystemMaster, SelectedApplication }
public enum FullscreenNotificationStyle { Minimal, WithControls }
public enum MonitorSelectionMode { Primary, ActiveWindow, Fixed }
public enum StartupLaunchMode { Island, Settings, SilentTray }
public enum CloseBehaviorSetting { MinimizeToTray, Exit }
