namespace MiaDock.Platform.Windows.Connectivity;

public sealed class NetworkRateCalculator
{
    private NetworkCounterSnapshot? _previous;

    public (double Download, double Upload)? Add(NetworkCounterSnapshot current)
    {
        var previous = _previous;
        _previous = current;
        if (previous is null || current.Timestamp <= previous.Timestamp ||
            current.ReceivedBytes < previous.ReceivedBytes || current.SentBytes < previous.SentBytes)
        {
            return null;
        }

        var seconds = (current.Timestamp - previous.Timestamp).TotalSeconds;
        return seconds <= 0
            ? null
            : ((current.ReceivedBytes - previous.ReceivedBytes) / seconds,
               (current.SentBytes - previous.SentBytes) / seconds);
    }

    public void Reset() => _previous = null;
}

public sealed record NetworkCounterSnapshot(ulong ReceivedBytes, ulong SentBytes, DateTimeOffset Timestamp);
