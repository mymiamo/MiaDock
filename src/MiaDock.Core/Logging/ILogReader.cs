namespace MiaDock.Core.Logging;

public interface ILogReader
{
    Task<IReadOnlyList<TechnicalLogEntry>> ReadLatestAsync(
        int maximumEntries = 250,
        CancellationToken cancellationToken = default);

    Task<LogStorageInfo> GetStorageInfoAsync(CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
