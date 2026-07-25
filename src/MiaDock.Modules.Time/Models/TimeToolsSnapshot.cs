namespace MiaDock.Modules.Time.Models;

public sealed record TimeToolsSnapshot(
    TimerRunState TimerState,
    TimeSpan TimerDuration,
    TimeSpan TimerRemaining,
    DateTimeOffset? TimerTargetUtc,
    bool IsStopwatchRunning,
    TimeSpan StopwatchElapsed,
    IReadOnlyList<TimeSpan> Laps)
{
    public static TimeToolsSnapshot Default { get; } = new(
        TimerRunState.Idle,
        TimeSpan.Zero,
        TimeSpan.Zero,
        null,
        false,
        TimeSpan.Zero,
        Array.Empty<TimeSpan>());

    public double TimerProgress => TimerDuration <= TimeSpan.Zero
        ? 0
        : Math.Clamp(1 - TimerRemaining.TotalMilliseconds / TimerDuration.TotalMilliseconds, 0, 1);
}
