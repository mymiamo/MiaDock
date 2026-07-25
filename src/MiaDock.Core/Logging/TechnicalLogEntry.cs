namespace MiaDock.Core.Logging;

public sealed record TechnicalLogEntry(
    DateTimeOffset TimestampUtc,
    TechnicalLogLevel Level,
    string EventId,
    string Category,
    string Message,
    string SessionId,
    string? ExceptionType = null,
    int? HResult = null,
    string? StackTrace = null,
    IReadOnlyDictionary<string, string>? Properties = null);

public sealed record LogStorageInfo(int FileCount, long TotalBytes)
{
    public static LogStorageInfo Empty { get; } = new(0, 0);
}
