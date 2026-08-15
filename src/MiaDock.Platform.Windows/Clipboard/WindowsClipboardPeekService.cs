using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MiaDock.Core.Clipboard;
using MiaDock.Core.Logging;
using MiaDock.Core.Threading;
using MiaDock.Platform.Windows.Lifecycle;
using Windows.Storage;
using Windows.System;

namespace MiaDock.Platform.Windows.Clipboard;

public sealed class WindowsClipboardPeekService : IClipboardPeekService
{
    private const int MaximumImageCacheBytes = 64 * 1024 * 1024;
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan SensitiveLifetime = TimeSpan.FromMinutes(5);
    private readonly IUiDispatcher _dispatcher;
    private readonly IClipboardPeekSettings _settings;
    private readonly ILogService _log;
    private readonly IWindowsSessionLockStateService _sessionLock;
    private readonly TimeProvider _timeProvider;
    private readonly IClipboardPlatformAdapter _platform;
    private readonly List<ClipboardPeekItem> _history = [];
    private readonly Dictionary<string, byte[]> _imageCache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _imageCacheOrder = [];
    private readonly object _gate = new();
    private CancellationTokenSource? _lifetime;
    private SensitiveEntry? _sensitive;
    private CancellationTokenSource? _sensitiveExpiry;
    private string? _lastFingerprint;
    private DateTimeOffset _lastCapturedAt;
    private string? _selfWriteFingerprint;
    private DateTimeOffset _selfWriteUntil;
    private int _imageCacheBytes;
    private bool _started;
    private bool _disposed;
    private long _lifecycleGeneration;
    private long _captureRequest;
    private long _processedRequest;
    private int _captureWorkerActive;

    public WindowsClipboardPeekService(
        IUiDispatcher dispatcher,
        IClipboardPeekSettings settings,
        ILogService log,
        IWindowsSessionLockStateService sessionLock,
        TimeProvider timeProvider)
        : this(dispatcher, settings, log, sessionLock, timeProvider, new WindowsClipboardPlatformAdapter())
    {
    }

    internal WindowsClipboardPeekService(
        IUiDispatcher dispatcher,
        IClipboardPeekSettings settings,
        ILogService log,
        IWindowsSessionLockStateService sessionLock,
        TimeProvider timeProvider,
        IClipboardPlatformAdapter platform)
    {
        _dispatcher = dispatcher;
        _settings = settings;
        _log = log;
        _sessionLock = sessionLock;
        _timeProvider = timeProvider;
        _platform = platform;
    }

    public ClipboardPeekState Current { get; private set; } = ClipboardPeekState.Empty;
    public event EventHandler<ClipboardPeekState>? StateChanged;
    public event EventHandler<ClipboardPeekItem>? ItemCaptured;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        long generation;
        lock (_gate)
        {
            if (_started) return;
            _started = true;
            generation = ++_lifecycleGeneration;
            _lifetime = new CancellationTokenSource();
            _platform.ContentChanged += OnContentChanged;
            _settings.Changed += OnSettingsChanged;
            _sessionLock.LockStateChanged += OnLockStateChanged;
            _platform.Start();
        }

        try
        {
            var snapshot = await _platform.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (snapshot is not null)
                await ApplySnapshotAsync(snapshot, initial: true, generation, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("initial-read", exception);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CancellationTokenSource? lifetime;
        lock (_gate)
        {
            if (!_started) return;
            _started = false;
            _lifecycleGeneration++;
            lifetime = _lifetime;
            _lifetime = null;
            _platform.Stop();
            _platform.ContentChanged -= OnContentChanged;
            _settings.Changed -= OnSettingsChanged;
            _sessionLock.LockStateChanged -= OnLockStateChanged;
            ClearPrivateStateLocked(clearCurrent: true);
        }
        lifetime?.Cancel();
        lifetime?.Dispose();
        await DispatchAsync(() => Publish(ClipboardPeekState.Empty), cancellationToken).ConfigureAwait(false);
    }

    public async Task<ClipboardPeekActionResult> ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await DispatchAsync(() =>
        {
            lock (_gate)
            {
                _history.Clear();
                ClearHistoryImagePayloadsLocked();
            }
            Publish(Current with { History = Array.Empty<ClipboardPeekItem>(), IsInitialSnapshot = false });
        }, cancellationToken).ConfigureAwait(false);
        return ClipboardPeekActionResult.Succeeded;
    }

    public Task<ClipboardPeekActionResult> CopyAsync(
        ClipboardPeekItem item,
        CancellationToken cancellationToken = default)
    {
        if (item.IsSensitive || string.IsNullOrEmpty(item.RawText))
            return Task.FromResult(ClipboardPeekActionResult.Unavailable);
        return CopyTextAsync(item.RawText, cancellationToken);
    }

    public async Task<ClipboardPeekActionResult> CopyTextAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return ClipboardPeekActionResult.Unavailable;
        try
        {
            var fingerprint = FingerprintText(text);
            lock (_gate)
            {
                _selfWriteFingerprint = fingerprint;
                _selfWriteUntil = _timeProvider.GetUtcNow().Add(DuplicateWindow);
            }
            await _platform.WriteTextAsync(text, cancellationToken).ConfigureAwait(false);
            return ClipboardPeekActionResult.Succeeded;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("copy", exception);
            return MapFailure(exception);
        }
    }

    public async Task<ClipboardPeekActionResult> OpenAsync(
        ClipboardPeekItem item,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            bool launched;
            if (item.Uri is { Scheme: "http" or "https" } uri)
                launched = await Launcher.LaunchUriAsync(uri);
            else if (!string.IsNullOrWhiteSpace(item.EmailAddress))
                launched = await Launcher.LaunchUriAsync(new Uri($"mailto:{Uri.EscapeDataString(item.EmailAddress)}"));
            else if (!string.IsNullOrWhiteSpace(item.FilePath) && Path.IsPathFullyQualified(item.FilePath) && Directory.Exists(item.FilePath))
                launched = await Launcher.LaunchFolderAsync(await StorageFolder.GetFolderFromPathAsync(item.FilePath));
            else if (!string.IsNullOrWhiteSpace(item.FilePath) && Path.IsPathFullyQualified(item.FilePath) && File.Exists(item.FilePath))
                launched = await Launcher.LaunchFileAsync(await StorageFile.GetFileFromPathAsync(item.FilePath));
            else
                return ClipboardPeekActionResult.Unavailable;
            return launched ? ClipboardPeekActionResult.Succeeded : ClipboardPeekActionResult.Failed;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("open", exception);
            return MapFailure(exception);
        }
    }

    public async Task<ClipboardPeekActionResult> OpenContainingFolderAsync(
        ClipboardPeekItem item,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = item.FilePath;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return ClipboardPeekActionResult.Unavailable;
        var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return ClipboardPeekActionResult.Unavailable;
        try
        {
            return await Launcher.LaunchFolderAsync(await StorageFolder.GetFolderFromPathAsync(folder))
                ? ClipboardPeekActionResult.Succeeded
                : ClipboardPeekActionResult.Failed;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("open-folder", exception);
            return MapFailure(exception);
        }
    }

    public async Task<ClipboardPeekActionResult> SaveImageAsync(
        ClipboardPeekItem item,
        nint ownerWindow,
        CancellationToken cancellationToken = default)
    {
        byte[]? png;
        lock (_gate) _imageCache.TryGetValue(item.Id, out png);
        if (item.Type != ClipboardPeekContentType.Image || png is null || ownerWindow == 0)
            return ClipboardPeekActionResult.Unavailable;
        try
        {
            return await _platform.SavePngAsync(png, ownerWindow, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogFailure("save-image", exception);
            return MapFailure(exception);
        }
    }

    public Task<ClipboardPeekRevealResult> RevealSensitiveAsync(
        string itemId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_sensitive is null || !string.Equals(_sensitive.ItemId, itemId, StringComparison.Ordinal) ||
                _timeProvider.GetUtcNow() >= _sensitive.ExpiresAt)
            {
                ClearSensitiveLocked();
                return Task.FromResult(ClipboardPeekRevealResult.Unavailable);
            }

            var value = new string(_sensitive.Value);
            ClearSensitiveLocked();
            return Task.FromResult(new ClipboardPeekRevealResult(ClipboardPeekActionResult.Succeeded, value));
        }
    }

    private void OnContentChanged(object? sender, EventArgs args)
    {
        lock (_gate) ClearSensitiveLocked();
        Interlocked.Increment(ref _captureRequest);
        StartCaptureWorker();
    }

    private void StartCaptureWorker()
    {
        if (Interlocked.CompareExchange(ref _captureWorkerActive, 1, 0) != 0) return;
        _ = ProcessCaptureQueueAsync();
    }

    private async Task ProcessCaptureQueueAsync()
    {
        try
        {
            while (true)
            {
                long generation;
                CancellationToken cancellationToken;
                long request = Volatile.Read(ref _captureRequest);
                lock (_gate)
                {
                    if (!_started || _disposed || _lifetime is null) return;
                    generation = _lifecycleGeneration;
                    cancellationToken = _lifetime.Token;
                }

                ClipboardPlatformSnapshot? snapshot;
                try
                {
                    snapshot = await _platform.ReadAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    LogFailure("read", exception);
                    snapshot = null;
                }

                if (request != Volatile.Read(ref _captureRequest)) continue;
                if (snapshot is not null)
                    await ApplySnapshotAsync(snapshot, initial: false, generation, cancellationToken).ConfigureAwait(false);
                Volatile.Write(ref _processedRequest, request);
                if (request == Volatile.Read(ref _captureRequest)) return;
            }
        }
        finally
        {
            Interlocked.Exchange(ref _captureWorkerActive, 0);
            var shouldRestart = false;
            lock (_gate)
            {
                shouldRestart = _started && !_disposed;
            }
            if (shouldRestart) StartCaptureWorkerIfPending();
        }
    }

    private void StartCaptureWorkerIfPending()
    {
        if (Volatile.Read(ref _captureRequest) != Volatile.Read(ref _processedRequest)) StartCaptureWorker();
    }

    private async Task ApplySnapshotAsync(
        ClipboardPlatformSnapshot snapshot,
        bool initial,
        long generation,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var captured = CreateItem(snapshot, now);
        if (captured is null) return;
        var (item, fingerprint, sensitiveValue, imagePayload) = captured.Value;
        await DispatchAsync(() =>
        {
            lock (_gate)
            {
                if (!_started || _disposed || generation != _lifecycleGeneration) return;
                ClearSensitiveLocked();
                if (!initial && _selfWriteFingerprint == fingerprint && now <= _selfWriteUntil)
                {
                    _selfWriteFingerprint = null;
                    return;
                }
                if (!initial && _lastFingerprint == fingerprint && now - _lastCapturedAt <= DuplicateWindow)
                {
                    _lastCapturedAt = now;
                    return;
                }

                _lastFingerprint = fingerprint;
                _lastCapturedAt = now;
                if (sensitiveValue is not null)
                {
                    _sensitive = new SensitiveEntry(item.Id, sensitiveValue.ToCharArray(), now.Add(SensitiveLifetime));
                    _sensitiveExpiry = new CancellationTokenSource();
                    _ = ExpireSensitiveAsync(item.Id, _sensitiveExpiry.Token);
                }
                if (imagePayload is not null) AddImagePayloadLocked(item.Id, imagePayload);
                if (!item.IsSensitive && _settings.Current.HistoryLimit > 0)
                {
                    _history.Insert(0, item);
                    TrimHistoryLocked();
                }
                Publish(new ClipboardPeekState(item, _history.ToArray(), initial));
            }
            if (!initial) ItemCaptured?.Invoke(this, item);
        }, cancellationToken).ConfigureAwait(false);
    }

    private (ClipboardPeekItem Item, string Fingerprint, string? SensitiveValue, byte[]? ImagePayload)? CreateItem(
        ClipboardPlatformSnapshot snapshot,
        DateTimeOffset createdAt)
    {
        switch (snapshot.Kind)
        {
            case ClipboardPlatformContentKind.Text:
            {
                var text = snapshot.Text ?? string.Empty;
                var item = ClipboardPeekClassifier.ClassifyText(text, createdAt);
                return (item, FingerprintText(text), item.IsSensitive ? text : null, null);
            }
            case ClipboardPlatformContentKind.StorageItems when snapshot.StorageItems is { Count: > 1 } items:
            {
                var item = new ClipboardPeekItem(
                    Guid.NewGuid().ToString("N"), ClipboardPeekContentType.Unknown,
                    items.Count.ToString(CultureInfo.InvariantCulture), null, createdAt,
                    "StorageItems.Multiple", false, ClipboardPeekCapabilities.None, ItemCount: items.Count);
                return (item, FingerprintText($"storage-count:{items.Count}"), null, null);
            }
            case ClipboardPlatformContentKind.StorageItems when snapshot.StorageItems is { Count: 1 } items:
            {
                var storage = items[0];
                var path = storage.Path;
                var type = storage.IsFolder ? ClipboardPeekContentType.Folder : ClipboardPeekContentType.File;
                var capabilities = ClipboardPeekCapabilities.None;
                if (!string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) &&
                    (File.Exists(path) || Directory.Exists(path)))
                    capabilities = ClipboardPeekCapabilities.Copy | ClipboardPeekCapabilities.Open |
                                   ClipboardPeekCapabilities.OpenFolder;
                var item = new ClipboardPeekItem(
                    Guid.NewGuid().ToString("N"), type, storage.Name, path, createdAt,
                    "StorageItems", false, capabilities, FilePath: path);
                return (item, FingerprintText($"storage:{path ?? storage.Name}"), null, null);
            }
            case ClipboardPlatformContentKind.Image when snapshot.Image is { } image:
            {
                var id = Guid.NewGuid().ToString("N");
                var capabilities = image.FullPng is null
                    ? ClipboardPeekCapabilities.None
                    : ClipboardPeekCapabilities.SaveImage;
                var item = new ClipboardPeekItem(
                    id, ClipboardPeekContentType.Image, $"{image.Width} × {image.Height}", null,
                    createdAt, "Bitmap", false, capabilities,
                    Image: new ClipboardImagePreview(image.Width, image.Height, "PNG", image.ThumbnailPng));
                var fingerprintBytes = image.FullPng ?? image.ThumbnailPng ?? Encoding.UTF8.GetBytes(item.DisplayText);
                return (item, FingerprintBytes(fingerprintBytes), null, image.FullPng);
            }
            default:
                return null;
        }
    }

    private void OnSettingsChanged(object? sender, ClipboardPeekOptions options)
    {
        void Apply()
        {
            lock (_gate)
            {
                if (!_started || _disposed) return;
                TrimHistoryLocked();
                Publish(Current with { History = _history.ToArray(), IsInitialSnapshot = false });
            }
        }
        if (_dispatcher.HasThreadAccess) Apply(); else _dispatcher.TryEnqueue(Apply);
    }

    private void OnLockStateChanged(object? sender, bool isLocked)
    {
        if (!isLocked) return;
        lock (_gate) ClearSensitiveLocked();
    }

    private void TrimHistoryLocked()
    {
        var limit = NormalizeHistoryLimit(_settings.Current.HistoryLimit);
        while (_history.Count > limit)
        {
            var removed = _history[^1];
            _history.RemoveAt(_history.Count - 1);
            RemoveImagePayloadLocked(removed.Id);
        }
        if (limit == 0) ClearHistoryImagePayloadsLocked();
    }

    internal static int NormalizeHistoryLimit(int value)
    {
        ReadOnlySpan<int> allowed = [0, 5, 10, 20];
        var best = allowed[0];
        var distance = int.MaxValue;
        foreach (var candidate in allowed)
        {
            var nextDistance = Math.Abs(candidate - value);
            if (nextDistance < distance || nextDistance == distance && candidate > best)
            {
                best = candidate;
                distance = nextDistance;
            }
        }
        return best;
    }

    private void AddImagePayloadLocked(string itemId, byte[] payload)
    {
        _imageCache[itemId] = payload;
        _imageCacheOrder.AddLast(itemId);
        _imageCacheBytes += payload.Length;
        while (_imageCacheBytes > MaximumImageCacheBytes && _imageCacheOrder.First is { } oldest)
        {
            var evictedId = oldest.Value;
            RemoveImagePayloadLocked(evictedId);
            DisableSaveActionLocked(evictedId);
        }
    }

    private void DisableSaveActionLocked(string itemId)
    {
        for (var index = 0; index < _history.Count; index++)
            if (_history[index].Id == itemId)
                _history[index] = _history[index] with
                {
                    AvailableActions = _history[index].AvailableActions & ~ClipboardPeekCapabilities.SaveImage
                };
        if (Current.CurrentItem?.Id == itemId)
            Current = Current with
            {
                CurrentItem = Current.CurrentItem with
                {
                    AvailableActions = Current.CurrentItem.AvailableActions & ~ClipboardPeekCapabilities.SaveImage
                }
            };
    }

    private void RemoveImagePayloadLocked(string itemId)
    {
        if (_imageCache.Remove(itemId, out var payload)) _imageCacheBytes -= payload.Length;
        _imageCacheOrder.Remove(itemId);
    }

    private void ClearImageCacheLocked()
    {
        _imageCache.Clear();
        _imageCacheOrder.Clear();
        _imageCacheBytes = 0;
    }

    private void ClearHistoryImagePayloadsLocked()
    {
        var currentId = Current.CurrentItem?.Id;
        foreach (var itemId in _imageCache.Keys.Where(id => id != currentId).ToArray())
            RemoveImagePayloadLocked(itemId);
    }

    private void ClearSensitiveLocked()
    {
        _sensitiveExpiry?.Cancel();
        _sensitiveExpiry?.Dispose();
        _sensitiveExpiry = null;
        if (_sensitive is not null) Array.Clear(_sensitive.Value);
        _sensitive = null;
    }

    private async Task ExpireSensitiveAsync(string itemId, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SensitiveLifetime, cancellationToken).ConfigureAwait(false);
            lock (_gate)
            {
                if (_sensitive?.ItemId == itemId) ClearSensitiveLocked();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void ClearPrivateStateLocked(bool clearCurrent)
    {
        ClearSensitiveLocked();
        ClearImageCacheLocked();
        _history.Clear();
        _lastFingerprint = null;
        _selfWriteFingerprint = null;
        if (clearCurrent) Current = ClipboardPeekState.Empty;
    }

    private void Publish(ClipboardPeekState state)
    {
        Current = state;
        StateChanged?.Invoke(this, state);
    }

    private Task DispatchAsync(Action action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_dispatcher.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcher.TryEnqueue(() =>
            {
                try { action(); completion.SetResult(); }
                catch (Exception exception) { completion.SetException(exception); }
            }))
            completion.SetException(new InvalidOperationException("UI dispatcher rejected clipboard update."));
        return completion.Task.WaitAsync(cancellationToken);
    }

    private static string FingerprintText(string value) =>
        FingerprintBytes(Encoding.UTF8.GetBytes(value));

    private static string FingerprintBytes(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value));

    private static ClipboardPeekActionResult MapFailure(Exception exception) => exception switch
    {
        UnauthorizedAccessException => ClipboardPeekActionResult.AccessDenied,
        NotSupportedException => ClipboardPeekActionResult.Unsupported,
        InvalidOperationException or IOException or ArgumentException => ClipboardPeekActionResult.Failed,
        _ => ClipboardPeekActionResult.Failed
    };

    private void LogFailure(string operation, Exception exception) => _log.Write(
        TechnicalLogLevel.Warning,
        TechnicalEventIds.DeviceStatusUnavailable,
        "ClipboardPeek",
        "Clipboard operation failed safely.",
        null,
        new Dictionary<string, object?>
        {
            ["operation"] = operation,
            ["errorType"] = exception.GetType().Name
        });

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync(CancellationToken.None).ConfigureAwait(false);
        _disposed = true;
        await _platform.DisposeAsync().ConfigureAwait(false);
    }

    private sealed record SensitiveEntry(string ItemId, char[] Value, DateTimeOffset ExpiresAt);
}
