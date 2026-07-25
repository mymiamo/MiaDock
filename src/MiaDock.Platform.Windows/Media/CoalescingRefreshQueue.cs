namespace MiaDock.Platform.Windows.Media;

public sealed class CoalescingRefreshQueue : IAsyncDisposable
{
    private readonly object _sync = new();
    private readonly Func<CancellationToken, Task> _refresh;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly TimeSpan _minimumInterval;
    private bool _isRunning;
    private bool _isPending;
    private Task _runner = Task.CompletedTask;
    private long _lastRefreshTimestamp;

    public CoalescingRefreshQueue(
        Func<CancellationToken, Task> refresh,
        TimeSpan minimumInterval = default)
    {
        _refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
        if (minimumInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumInterval));
        }

        _minimumInterval = minimumInterval;
    }

    public Exception? LastFailure { get; private set; }

    public void Request()
    {
        lock (_sync)
        {
            if (_lifetime.IsCancellationRequested)
            {
                return;
            }

            _isPending = true;
            if (_isRunning)
            {
                return;
            }

            _isRunning = true;
            _runner = RunAsync();
        }
    }

    public Task WaitForIdleAsync()
    {
        lock (_sync)
        {
            return _runner;
        }
    }

    private async Task RunAsync()
    {
        while (true)
        {
            lock (_sync)
            {
                _isPending = false;
            }

            try
            {
                if (_minimumInterval > TimeSpan.Zero && _lastRefreshTimestamp != 0)
                {
                    var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(_lastRefreshTimestamp);
                    var delay = _minimumInterval - elapsed;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay, _lifetime.Token).ConfigureAwait(false);
                    }
                }

                _lastRefreshTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                await _refresh(_lifetime.Token).ConfigureAwait(false);
                LastFailure = null;
            }
            catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LastFailure = exception;
            }

            lock (_sync)
            {
                if (_isPending)
                {
                    continue;
                }

                _isRunning = false;
                return;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        Task runner;
        lock (_sync)
        {
            runner = _runner;
        }

        await runner.ConfigureAwait(false);
        _lifetime.Dispose();
    }
}
