namespace MiaDock.Core.Focus;

public sealed record FocusSchedule(
    string Id,
    bool IsEnabled,
    FocusDays Days,
    int StartMinute,
    int EndMinute);
