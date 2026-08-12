namespace MiaDock.Core.Lifecycle;

public sealed class CrashStateRecord
{
    public bool PendingCrash { get; set; }

    public bool SessionActive { get; set; }

    public DateTimeOffset? CrashedAtUtc { get; set; }

    public string? ExceptionType { get; set; }

    public string? ExceptionMessage { get; set; }

    public int RestartCount { get; set; }

    public DateTimeOffset? LastRestartUtc { get; set; }
}
