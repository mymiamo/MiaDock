using MiaDock.Modules.Transfers;

namespace MiaDock.Platform.Windows.Transfers;

public sealed class ProviderRateLimiter
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(1);
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private readonly Dictionary<string, Queue<DateTimeOffset>> _updates = new(StringComparer.Ordinal);

    public ProviderRateLimiter(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool TryAcquire(string providerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (!_updates.TryGetValue(providerId, out var history))
            {
                history = new Queue<DateTimeOffset>();
                _updates[providerId] = history;
            }

            while (history.TryPeek(out var timestamp) && now - timestamp >= Window)
            {
                history.Dequeue();
            }

            if (history.Count >= TransferProtocol.MaximumUpdatesPerSecond) return false;
            history.Enqueue(now);
            return true;
        }
    }
}
