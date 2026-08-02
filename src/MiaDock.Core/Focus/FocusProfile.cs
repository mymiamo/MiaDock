namespace MiaDock.Core.Focus;

public sealed record FocusProfile(
    string Id,
    FocusProfileKind Kind,
    string? CustomName,
    string IconKey,
    string Color,
    int? DefaultDurationMinutes,
    FocusProfileBehavior Behavior,
    IReadOnlyList<FocusSchedule> Schedules,
    IReadOnlyList<FocusActivationRule> ActivationRules);
