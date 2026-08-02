namespace MiaDock.Core.Focus;

public enum FocusProfileKind
{
    Work,
    Gaming,
    Sleep,
    DoNotDisturb,
    Custom
}

public enum FocusDockVisibility
{
    UseGlobalSetting,
    AlwaysVisible,
    EventsOnly,
    Hidden
}

[Flags]
public enum FocusDays
{
    None = 0,
    Monday = 1 << 0,
    Tuesday = 1 << 1,
    Wednesday = 1 << 2,
    Thursday = 1 << 3,
    Friday = 1 << 4,
    Saturday = 1 << 5,
    Sunday = 1 << 6,
    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Weekend = Saturday | Sunday,
    EveryDay = Weekdays | Weekend
}

public enum FocusActivationRuleKind
{
    ApplicationRunning,
    ApplicationForeground,
    FullscreenApplication
}

public enum FocusActivationSource
{
    Manual,
    Schedule,
    Automation,
    Restored
}
