namespace MiaDock.Platform.Windows.Media;

internal sealed class GenerationSessionAccessCoordinator<TSession> : IAsyncDisposable
    where TSession : class
{
    private readonly object _gate = new();
    private SessionLease? _current;
    private long _generation;
    private bool _disposed;

    public SessionLease? Switch(TSession? session)
    {
        SessionLease? previous;
        SessionLease? next;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            previous = _current;
            next = session is null ? null : new SessionLease(session, ++_generation);
            if (session is null)
            {
                _generation++;
            }
            _current = next;
        }
        previous?.Retire();
        return next;
    }

    public SessionLease? Capture()
    {
        lock (_gate)
        {
            return _disposed ? null : _current;
        }
    }

    public bool IsCurrent(SessionLease lease)
    {
        lock (_gate)
        {
            return !_disposed && ReferenceEquals(lease, _current) && !lease.IsRetired;
        }
    }

    public async Task<TResult> ExecuteAsync<TResult>(
        SessionLease lease,
        Func<TSession, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(operation);
        if (!lease.TryAcquire(out var sessionToken))
        {
            throw new OperationCanceledException("The media session is no longer active.");
        }

        var entered = false;
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                sessionToken);
            await lease.AccessGate.WaitAsync(linked.Token).ConfigureAwait(false);
            entered = true;
            if (!IsCurrent(lease) || !lease.TryBeginNativeCall())
            {
                throw new OperationCanceledException(linked.Token);
            }

            var result = await operation(lease.Session, linked.Token).ConfigureAwait(false);
            linked.Token.ThrowIfCancellationRequested();
            if (!IsCurrent(lease))
            {
                throw new OperationCanceledException(linked.Token);
            }
            return result;
        }
        finally
        {
            if (entered)
            {
                lease.AccessGate.Release();
            }
            lease.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        SessionLease? current;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            current = _current;
            _current = null;
            _generation++;
        }
        current?.Retire();
        if (current is not null)
        {
            await current.WaitForIdleAsync().ConfigureAwait(false);
        }
    }

    internal sealed class SessionLease
    {
        private readonly object _stateGate = new();
        private readonly CancellationTokenSource _lifetime = new();
        private TaskCompletionSource? _idle;
        private int _activeOperations;
        private bool _retired;
        private bool _resourcesDisposed;

        internal SessionLease(TSession session, long generation)
        {
            Session = session;
            Generation = generation;
        }

        public TSession Session { get; }

        public long Generation { get; }

        internal SemaphoreSlim AccessGate { get; } = new(1, 1);

        public bool IsRetired
        {
            get
            {
                lock (_stateGate) return _retired;
            }
        }

        internal bool TryAcquire(out CancellationToken token)
        {
            lock (_stateGate)
            {
                if (_retired)
                {
                    token = new CancellationToken(true);
                    return false;
                }
                _activeOperations++;
                token = _lifetime.Token;
                return true;
            }
        }

        internal bool TryBeginNativeCall()
        {
            lock (_stateGate) return !_retired;
        }

        internal void Release()
        {
            TaskCompletionSource? idle = null;
            var dispose = false;
            lock (_stateGate)
            {
                _activeOperations--;
                if (_retired && _activeOperations == 0)
                {
                    idle = _idle;
                    dispose = !_resourcesDisposed;
                    if (dispose) _resourcesDisposed = true;
                }
            }
            idle?.TrySetResult();
            if (dispose) DisposeResources();
        }

        internal void Retire()
        {
            TaskCompletionSource? idle = null;
            var dispose = false;
            lock (_stateGate)
            {
                if (_retired) return;
                _retired = true;
                if (_activeOperations == 0)
                {
                    idle = _idle;
                    dispose = !_resourcesDisposed;
                    if (dispose) _resourcesDisposed = true;
                }
            }
            _lifetime.Cancel();
            idle?.TrySetResult();
            if (dispose) DisposeResources();
        }

        internal Task WaitForIdleAsync()
        {
            lock (_stateGate)
            {
                if (_activeOperations == 0)
                {
                    return Task.CompletedTask;
                }

                _idle ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                return _idle.Task;
            }
        }

        private void DisposeResources()
        {
            _lifetime.Dispose();
            AccessGate.Dispose();
        }
    }
}
