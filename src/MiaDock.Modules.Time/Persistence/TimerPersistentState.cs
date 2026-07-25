using MiaDock.Modules.Time.Models;

namespace MiaDock.Modules.Time.Persistence;

public sealed record TimerPersistentState(
    int SchemaVersion,
    TimerRunState State,
    long DurationTicks,
    long RemainingTicks,
    DateTimeOffset? TargetUtc,
    bool CompletionPending)
{
    public const int CurrentSchemaVersion = 1;

    public static TimerPersistentState Idle { get; } = new(
        CurrentSchemaVersion,
        TimerRunState.Idle,
        0,
        0,
        null,
        false);
}
