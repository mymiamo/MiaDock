using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using MiaDock.Core.Logging;

namespace MiaDock.Platform.Windows.Logging;

public sealed class JsonLinesLogService : ILogService, ILogReader, ILogArchiveService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Channel<QueueItem> _queue;
    private readonly SensitiveDataRedactor _redactor;
    private readonly LogRetentionPolicy _retention;
    private readonly TimeSpan _flushInterval;
    private readonly SemaphoreSlim _fileGate = new(1, 1);
    private readonly Task _writerTask;
    private readonly string _sessionId = Guid.NewGuid().ToString("N")[..12];
    private DateTimeOffset _lastMaintenanceUtc = DateTimeOffset.MinValue;
    private long _droppedEntryCount;
    private bool _disposed;

    public JsonLinesLogService(
        ILogPathProvider pathProvider,
        SensitiveDataRedactor redactor,
        LogRetentionPolicy? retention = null,
        TimeSpan? flushInterval = null)
    {
        ArgumentNullException.ThrowIfNull(pathProvider);
        _redactor = redactor ?? throw new ArgumentNullException(nameof(redactor));
        _retention = retention ?? LogRetentionPolicy.Default;
        _retention.Validate();
        _flushInterval = flushInterval ?? TimeSpan.FromSeconds(2);
        if (_flushInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(flushInterval));
        }

        LogDirectoryPath = pathProvider.GetLogDirectoryPath();
        _queue = Channel.CreateBounded<QueueItem>(new BoundedChannelOptions(512)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
        _writerTask = RunWriterAsync();
    }

    public string LogDirectoryPath { get; }

    public Exception? LastFailure { get; private set; }

    public long DroppedEntryCount => Interlocked.Read(ref _droppedEntryCount);

    public void Write(
        TechnicalLogLevel level,
        string eventId,
        string category,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            var entry = new TechnicalLogEntry(
                DateTimeOffset.UtcNow,
                level,
                _redactor.Redact(eventId),
                _redactor.Redact(category),
                _redactor.Redact(message),
                _sessionId,
                exception?.GetType().FullName,
                exception?.HResult,
                _redactor.Redact(exception?.StackTrace),
                _redactor.SanitizeProperties(properties));
            if (!_queue.Writer.TryWrite(QueueItem.ForEntry(entry)))
            {
                Interlocked.Increment(ref _droppedEntryCount);
            }
        }
        catch
        {
            Interlocked.Increment(ref _droppedEntryCount);
        }
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await _queue.Writer.WriteAsync(QueueItem.ForFlush(completion), cancellationToken);
        await completion.Task.WaitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TechnicalLogEntry>> ReadLatestAsync(
        int maximumEntries = 250,
        CancellationToken cancellationToken = default)
    {
        maximumEntries = Math.Clamp(maximumEntries, 1, 1000);
        await FlushAsync(cancellationToken);
        await _fileGate.WaitAsync(cancellationToken);
        try
        {
            if (!Directory.Exists(LogDirectoryPath))
            {
                return Array.Empty<TechnicalLogEntry>();
            }

            var result = new List<TechnicalLogEntry>(maximumEntries);
            foreach (var file in GetLogFiles().OrderByDescending(File.GetLastWriteTimeUtc))
            {
                var lines = await File.ReadAllLinesAsync(file, cancellationToken);
                for (var index = lines.Length - 1; index >= 0 && result.Count < maximumEntries; index--)
                {
                    try
                    {
                        if (JsonSerializer.Deserialize<TechnicalLogEntry>(lines[index], SerializerOptions) is { } entry)
                        {
                            result.Add(entry);
                        }
                    }
                    catch (JsonException)
                    {
                    }
                }

                if (result.Count >= maximumEntries)
                {
                    break;
                }
            }

            return result;
        }
        finally
        {
            _fileGate.Release();
        }
    }

    public async Task<LogStorageInfo> GetStorageInfoAsync(CancellationToken cancellationToken = default)
    {
        await FlushAsync(cancellationToken);
        await _fileGate.WaitAsync(cancellationToken);
        try
        {
            var files = GetLogFiles().Select(path => new FileInfo(path)).ToArray();
            return new LogStorageInfo(files.Length, files.Sum(file => file.Length));
        }
        finally
        {
            _fileGate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await FlushAsync(cancellationToken);
        await _fileGate.WaitAsync(cancellationToken);
        try
        {
            foreach (var file in GetLogFiles())
            {
                File.Delete(file);
            }
        }
        finally
        {
            _fileGate.Release();
        }
    }

    public async Task ExportAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        await FlushAsync(cancellationToken);
        await _fileGate.WaitAsync(cancellationToken);
        try
        {
            var destinationDirectory = Path.GetDirectoryName(destinationPath)
                ?? throw new InvalidOperationException("The export destination has no directory.");
            Directory.CreateDirectory(destinationDirectory);
            await using var stream = new FileStream(destinationPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, true);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
            var files = GetLogFiles().ToArray();
            foreach (var file in files)
            {
                var entry = archive.CreateEntry(Path.GetFileName(file), CompressionLevel.Fastest);
                await using var destination = entry.Open();
                await using var source = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, true);
                await source.CopyToAsync(destination, cancellationToken);
            }

            var manifest = archive.CreateEntry("export-manifest.json", CompressionLevel.Fastest);
            await using var manifestStream = manifest.Open();
            await JsonSerializer.SerializeAsync(manifestStream, new
            {
                ExportedAtUtc = DateTimeOffset.UtcNow,
                FileCount = files.Length,
                Format = "MiaDock technical logs v1"
            }, cancellationToken: cancellationToken);
        }
        finally
        {
            _fileGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await FlushAsync();
        _disposed = true;
        _queue.Writer.TryComplete();
        await _writerTask;
        _fileGate.Dispose();
    }

    private async Task RunWriterAsync()
    {
        var batch = new List<TechnicalLogEntry>(64);
        while (true)
        {
            var canReadTask = _queue.Reader.WaitToReadAsync().AsTask();
            if (await Task.WhenAny(canReadTask, Task.Delay(_flushInterval)) != canReadTask)
            {
                await WriteBatchSafelyAsync(batch);
                batch.Clear();
                continue;
            }

            if (!await canReadTask)
            {
                break;
            }

            while (_queue.Reader.TryRead(out var item))
            {
                if (item.Entry is { } entry)
                {
                    batch.Add(entry);
                }

                if (batch.Count >= 64 || item.FlushCompletion is not null)
                {
                    await WriteBatchSafelyAsync(batch);
                    batch.Clear();
                }

                item.FlushCompletion?.TrySetResult();
            }
        }

        if (batch.Count > 0)
        {
            await WriteBatchSafelyAsync(batch);
        }
    }

    private async Task WriteBatchSafelyAsync(IReadOnlyList<TechnicalLogEntry> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        try
        {
            await _fileGate.WaitAsync();
            try
            {
                Directory.CreateDirectory(LogDirectoryPath);
                foreach (var entry in entries)
                {
                    var line = JsonSerializer.Serialize(entry, SerializerOptions) + Environment.NewLine;
                    var path = GetActiveLogFile(Encoding.UTF8.GetByteCount(line));
                    await File.AppendAllTextAsync(path, line, new UTF8Encoding(false));
                }

                MaintainFilesIfNeeded();
                LastFailure = null;
            }
            finally
            {
                _fileGate.Release();
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            LastFailure = exception;
        }
    }

    private string GetActiveLogFile(int incomingBytes)
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var candidates = Directory.GetFiles(LogDirectoryPath, $"miadock-{date}-*.ndjson")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var index = candidates.Length == 0
            ? 0
            : int.Parse(Path.GetFileNameWithoutExtension(candidates[^1]).Split('-')[2]);
        var path = Path.Combine(LogDirectoryPath, $"miadock-{date}-{index:000}.ndjson");
        if (File.Exists(path) && new FileInfo(path).Length + incomingBytes > _retention.MaximumFileBytes)
        {
            path = Path.Combine(LogDirectoryPath, $"miadock-{date}-{++index:000}.ndjson");
        }

        return path;
    }

    private void MaintainFilesIfNeeded()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastMaintenanceUtc < TimeSpan.FromHours(1))
        {
            return;
        }

        _lastMaintenanceUtc = now;
        var files = GetLogFiles()
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ToArray();
        foreach (var file in files.Where((file, index) =>
                     index >= _retention.MaximumFiles || now - file.LastWriteTimeUtc > _retention.MaximumAge))
        {
            try
            {
                file.Delete();
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private string[] GetLogFiles() => Directory.Exists(LogDirectoryPath)
        ? Directory.GetFiles(LogDirectoryPath, "miadock-*.ndjson")
        : Array.Empty<string>();

    private sealed record QueueItem(TechnicalLogEntry? Entry, TaskCompletionSource? FlushCompletion)
    {
        internal static QueueItem ForEntry(TechnicalLogEntry entry) => new(entry, null);
        internal static QueueItem ForFlush(TaskCompletionSource completion) => new(null, completion);
    }
}
