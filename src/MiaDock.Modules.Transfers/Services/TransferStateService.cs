using MiaDock.Modules.Transfers.Models;
using MiaDock.Core.Threading;

namespace MiaDock.Modules.Transfers.Services;

public sealed class TransferStateService : ITransferStateService
{
    public static readonly TimeSpan WaitingThreshold = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan DisconnectedThreshold = TimeSpan.FromSeconds(30);
    private const int MaximumPendingSnapshotUpdates = 128;

    private readonly ITransferProgressProvider _provider;
    private readonly TimeProvider _timeProvider;
    private readonly IUiDispatcher _dispatcher;
    private readonly object _gate = new();
    private readonly object _publishGate = new();
    private readonly Dictionary<string, TransferSnapshot> _active = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TransferSnapshot> _pendingSnapshots = new(StringComparer.Ordinal);
    private IReadOnlyList<TransferSnapshot>? _pendingActiveTransfers;
    private CancellationTokenSource? _heartbeatCancellation;
    private Task? _heartbeatTask;
    private int _publishDispatchPending;
    private bool _hasPendingActiveTransfers;
    private bool _started;
    private bool _disposed;

    public TransferStateService(
        ITransferProgressProvider provider,
        TimeProvider? timeProvider = null,
        IUiDispatcher? dispatcher = null)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _dispatcher = dispatcher ?? ImmediateUiDispatcher.Instance;
        _provider.MessageReceived += OnMessageReceived;
    }

    public IReadOnlyList<TransferSnapshot> ActiveTransfers
    {
        get
        {
            lock (_gate)
            {
                return GetActiveTransfers();
            }
        }
    }

    public event EventHandler<IReadOnlyList<TransferSnapshot>>? TransfersChanged;
    public event EventHandler<TransferSnapshot>? SnapshotChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_gate)
        {
            if (_started) return;
            _started = true;
        }

        try
        {
            await _provider.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            lock (_gate) _started = false;
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? heartbeatCancellation;
        Task? heartbeatTask;
        lock (_gate)
        {
            if (!_started) return;
            _started = false;
            heartbeatCancellation = _heartbeatCancellation;
            heartbeatTask = _heartbeatTask;
            _heartbeatCancellation = null;
            _heartbeatTask = null;
            _active.Clear();
        }

        heartbeatCancellation?.Cancel();
        if (heartbeatTask is not null)
        {
            try { await heartbeatTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        heartbeatCancellation?.Dispose();
        await _provider.StopAsync(cancellationToken).ConfigureAwait(false);
        QueuePublish([], Array.Empty<TransferSnapshot>());
    }

    public void EvaluateHeartbeats()
    {
        List<TransferSnapshot> changes = [];
        IReadOnlyList<TransferSnapshot>? active = null;
        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            foreach (var pair in _active.ToArray())
            {
                var age = now - pair.Value.LastUpdatedUtc;
                if (age >= DisconnectedThreshold)
                {
                    var disconnected = pair.Value with { Status = TransferStatus.Disconnected };
                    _active.Remove(pair.Key);
                    changes.Add(disconnected);
                }
                else if (age >= WaitingThreshold && pair.Value.Status != TransferStatus.Waiting)
                {
                    var waiting = pair.Value with { Status = TransferStatus.Waiting };
                    _active[pair.Key] = waiting;
                    changes.Add(waiting);
                }
            }

            if (changes.Count > 0) active = GetActiveTransfers();
        }

        if (changes.Count > 0 || active is not null)
        {
            QueuePublish(changes, active);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _provider.MessageReceived -= OnMessageReceived;
        await StopAsync().ConfigureAwait(false);
        _disposed = true;
    }

    private void OnMessageReceived(object? sender, TransferProgressMessage message)
    {
        if (!TransferProtocol.TryNormalize(message, out var normalized)) return;
        var now = _timeProvider.GetUtcNow();
        var snapshot = new TransferSnapshot(
            normalized.ProviderId,
            normalized.TransferId,
            normalized.SafeDisplayName,
            normalized.TransferredBytes,
            normalized.TotalBytes,
            normalized.Status,
            now);
        IReadOnlyList<TransferSnapshot> active;
        lock (_gate)
        {
            if (!_started) return;
            var key = CreateKey(snapshot.ProviderId, snapshot.TransferId);
            if (snapshot.IsTerminal) _active.Remove(key);
            else _active[key] = snapshot;
            active = GetActiveTransfers();
            if (_active.Count > 0) EnsureHeartbeatLoop();
        }

        QueuePublish([snapshot], active);
    }

    private void EnsureHeartbeatLoop()
    {
        if (_heartbeatTask is { IsCompleted: false }) return;
        _heartbeatCancellation = new CancellationTokenSource();
        var cancellation = _heartbeatCancellation;
        _heartbeatTask = RunHeartbeatLoopAsync(cancellation);
    }

    private async Task RunHeartbeatLoopAsync(CancellationTokenSource cancellation)
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), _timeProvider, cancellation.Token).ConfigureAwait(false);
                EvaluateHeartbeats();
                lock (_gate)
                {
                    if (_active.Count == 0) break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_heartbeatCancellation, cancellation))
                {
                    _heartbeatCancellation = null;
                    _heartbeatTask = null;
                }
            }
            cancellation.Dispose();
        }
    }

    private TransferSnapshot[] GetActiveTransfers() => _active.Values
        .OrderByDescending(item => item.LastUpdatedUtc)
        .ThenBy(item => item.ProviderId, StringComparer.Ordinal)
        .ThenBy(item => item.TransferId, StringComparer.Ordinal)
        .ToArray();

    private static string CreateKey(string providerId, string transferId) => $"{providerId}\u001F{transferId}";

    private void QueuePublish(
        IReadOnlyList<TransferSnapshot> snapshots,
        IReadOnlyList<TransferSnapshot>? activeTransfers)
    {
        if (_disposed)
        {
            return;
        }

        lock (_publishGate)
        {
            foreach (var snapshot in snapshots)
            {
                var key = CreateKey(snapshot.ProviderId, snapshot.TransferId);
                _pendingSnapshots[key] = snapshot;
            }

            while (_pendingSnapshots.Count > MaximumPendingSnapshotUpdates)
            {
                _pendingSnapshots.Remove(_pendingSnapshots.Keys.First());
            }

            if (activeTransfers is not null)
            {
                _pendingActiveTransfers = activeTransfers;
                _hasPendingActiveTransfers = true;
            }
        }

        QueuePublishDispatch();
    }

    private void QueuePublishDispatch()
    {
        if (_disposed ||
            Interlocked.CompareExchange(ref _publishDispatchPending, 1, 0) != 0)
        {
            return;
        }

        if (_dispatcher.HasThreadAccess)
        {
            DrainPendingPublications();
        }
        else if (!_dispatcher.TryEnqueue(DrainPendingPublications))
        {
            Volatile.Write(ref _publishDispatchPending, 0);
        }
    }

    private void DrainPendingPublications()
    {
        TransferSnapshot[] snapshots;
        IReadOnlyList<TransferSnapshot>? activeTransfers;
        bool hasActiveTransfers;
        lock (_publishGate)
        {
            snapshots = _pendingSnapshots.Values.ToArray();
            _pendingSnapshots.Clear();
            activeTransfers = _pendingActiveTransfers;
            hasActiveTransfers = _hasPendingActiveTransfers;
            _pendingActiveTransfers = null;
            _hasPendingActiveTransfers = false;
        }

        var shouldReschedule = false;
        try
        {
            if (!_disposed)
            {
                foreach (var snapshot in snapshots)
                {
                    SnapshotChanged?.Invoke(this, snapshot);
                }

                if (hasActiveTransfers)
                {
                    TransfersChanged?.Invoke(
                        this,
                        activeTransfers ?? Array.Empty<TransferSnapshot>());
                }
            }
        }
        finally
        {
            Volatile.Write(ref _publishDispatchPending, 0);
            lock (_publishGate)
            {
                shouldReschedule = !_disposed &&
                                   (_pendingSnapshots.Count > 0 ||
                                    _hasPendingActiveTransfers);
            }

            if (shouldReschedule)
            {
                QueuePublishDispatch();
            }
        }
    }

    private sealed class ImmediateUiDispatcher : IUiDispatcher
    {
        public static ImmediateUiDispatcher Instance { get; } = new();
        public bool HasThreadAccess => true;
        public bool TryEnqueue(Action callback)
        {
            callback();
            return true;
        }
    }
}
