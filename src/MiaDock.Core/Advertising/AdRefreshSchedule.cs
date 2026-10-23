namespace MiaDock.Core.Advertising;

public sealed class AdRefreshSchedule
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private TimeSpan _next;
    private bool _inFlight;
    public bool TryBegin(TimeSpan now)
    {
        if (_inFlight || now < _next) return false;
        _inFlight = true; _next = now + Interval;
        return true;
    }
    public void Complete() => _inFlight = false;
}
