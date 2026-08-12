using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
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
    private long _sequenceNumber;
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
                _redactor.SanitizeProperties(properties),
                Interlocked.Increment(ref _sequenceNumber),
                Environment.ProcessId,
                Environment.CurrentManagedThreadId,
                BuildExceptionChain(exception));
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

            var entries = ReadEntriesForExport(files, cancellationToken);
            var appVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
                ?? "unknown";
            await WriteJsonEntryAsync(archive, "export-manifest.json", new
            {
                ExportedAtUtc = DateTimeOffset.UtcNow,
                FileCount = files.Length,
                EntryCount = entries.Count,
                SessionCount = entries.Select(item => item.SessionId).Distinct(StringComparer.Ordinal).Count(),
                DroppedEntryCount,
                Format = "MiaDock technical logs v2",
                Product = "MiaDock",
                AppVersion = appVersion,
                Runtime = RuntimeInformation.FrameworkDescription,
                OperatingSystem = RuntimeInformation.OSDescription,
                OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                Is64BitProcess = Environment.Is64BitProcess,
                Culture = System.Globalization.CultureInfo.CurrentCulture.Name,
                UICulture = System.Globalization.CultureInfo.CurrentUICulture.Name,
                LoggingFailure = LastFailure is null
                    ? null
                    : new { Type = LastFailure.GetType().FullName, HResult = $"0x{LastFailure.HResult:X8}" }
            }, cancellationToken);

            var latest = entries
                .OrderByDescending(item => item.TimestampUtc)
                .ThenByDescending(item => item.SequenceNumber)
                .Take(250)
                .OrderBy(item => item.TimestampUtc)
                .ThenBy(item => item.SequenceNumber)
                .ToArray();
            await WriteJsonEntryAsync(archive, "diagnostic-timeline.json", latest, cancellationToken);

            var eventSummary = entries
                .GroupBy(item => new { item.Level, item.EventId })
                .Select(group => new
                {
                    Level = group.Key.Level.ToString(),
                    group.Key.EventId,
                    Count = group.Count(),
                    LastSeenUtc = group.Max(item => item.TimestampUtc)
                })
                .OrderByDescending(item => item.LastSeenUtc)
                .ToArray();
            await WriteJsonEntryAsync(archive, "event-summary.json", eventSummary, cancellationToken);

            var report = archive.CreateEntry("BUG-REPORT-README.txt", CompressionLevel.Fastest);
            await using var reportStream = report.Open();
            await using var writer = new StreamWriter(reportStream, new UTF8Encoding(false), leaveOpen: false);
            await writer.WriteAsync($"""
MiaDock güvenli tanılama paketi / safe diagnostics bundle

Uygulama sürümü / app version: {appVersion}
İşletim sistemi / operating system: {RuntimeInformation.OSDescription}
Mimari / architecture: OS={RuntimeInformation.OSArchitecture}, Process={RuntimeInformation.ProcessArchitecture}
Kayıt / entries: {entries.Count}; Oturum / sessions: {entries.Select(item => item.SessionId).Distinct(StringComparer.Ordinal).Count()}
Düşürülen kayıt / dropped entries: {DroppedEntryCount}

Hata bildirirken ekleyin / include with the bug report:
1. Sorunu yeniden oluşturma adımları / reproduction steps
2. Beklenen ve gerçekleşen davranış / expected and actual behavior
3. Yaklaşık hata saati ve saat dilimi / approximate failure time and time zone
4. Bu ZIP dosyası / this ZIP file

Gizlilik / privacy: medya başlığı, sanatçı, bildirim içeriği, kullanıcı adı ve kişisel dosya yolları kaydedilmez.
""");
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

    private string? BuildExceptionChain(Exception? exception)
    {
        if (exception is null)
        {
            return null;
        }

        var parts = new List<string>(5);
        for (var current = exception; current is not null && parts.Count < 5; current = current.InnerException)
        {
            parts.Add($"{current.GetType().FullName} (0x{current.HResult:X8})");
        }

        return _redactor.Redact(string.Join(" -> ", parts));
    }

    private static List<TechnicalLogEntry> ReadEntriesForExport(
        IEnumerable<string> files,
        CancellationToken cancellationToken)
    {
        var entries = new List<TechnicalLogEntry>();
        foreach (var file in files)
        {
            foreach (var line in File.ReadLines(file))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (JsonSerializer.Deserialize<TechnicalLogEntry>(line, SerializerOptions) is { } entry)
                    {
                        entries.Add(entry);
                    }
                }
                catch (JsonException)
                {
                }
            }
        }

        return entries;
    }

    private static async Task WriteJsonEntryAsync<T>(
        ZipArchive archive,
        string name,
        T value,
        CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, value, SerializerOptions, cancellationToken);
    }

    private sealed record QueueItem(TechnicalLogEntry? Entry, TaskCompletionSource? FlushCompletion)
    {
        internal static QueueItem ForEntry(TechnicalLogEntry entry) => new(entry, null);
        internal static QueueItem ForFlush(TaskCompletionSource completion) => new(null, completion);
    }
}
