namespace MiaDock.Core.Logging;

public interface ILogService : IAsyncDisposable
{
    string LogDirectoryPath { get; }

    Exception? LastFailure { get; }

    long DroppedEntryCount { get; }

    void Write(
        TechnicalLogLevel level,
        string eventId,
        string category,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, object?>? properties = null);

    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}
