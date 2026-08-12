namespace MiaDock.Core.Lifecycle;

public sealed class CrashStateRecord
{
    public bool PendingCrash { get; set; }

    public bool SessionActive { get; set; }

    public DateTimeOffset? CrashedAtUtc { get; set; }

    public string? ExceptionType { get; set; }

    public string? ExceptionMessage { get; set; }

    /// <summary>Bounded technical context retained only until the next launch consumes it.</summary>
    public string? ExceptionStackTrace { get; set; }

    public int? ExceptionHResult { get; set; }

    public string? ExceptionSource { get; set; }

    public int RestartCount { get; set; }

    public DateTimeOffset? LastRestartUtc { get; set; }
}
