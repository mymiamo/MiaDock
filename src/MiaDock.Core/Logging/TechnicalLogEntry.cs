using System.Text.Json.Serialization;

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
    IReadOnlyDictionary<string, string>? Properties = null,
    long SequenceNumber = 0,
    int ProcessId = 0,
    int ManagedThreadId = 0,
    string? ExceptionChain = null)
{
    [JsonIgnore]
    public string HResultText => HResult is { } value ? $"HRESULT: 0x{value:X8}" : string.Empty;

    [JsonIgnore]
    public string CorrelationText =>
        $"Oturum: {SessionId} · Sıra: {SequenceNumber} · İşlem/Thread: {ProcessId}/{ManagedThreadId}";

    [JsonIgnore]
    public string PropertiesText => Properties is { Count: > 0 }
        ? string.Join(" · ", Properties.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => $"{item.Key}={item.Value}"))
        : string.Empty;
}

public sealed record LogStorageInfo(int FileCount, long TotalBytes)
{
    public static LogStorageInfo Empty { get; } = new(0, 0);
}
