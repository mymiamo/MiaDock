namespace MiaDock.Platform.Windows.Threading;

internal sealed class CoalescingActionScheduler
{
    private readonly Func<Action, bool> _schedule;
    private readonly Action _action;
    private int _requested;
    private int _scheduledOrRunning;

    public CoalescingActionScheduler(Func<Action, bool> schedule, Action action)
    {
        _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public bool Request()
    {
        Interlocked.Exchange(ref _requested, 1);
        if (Interlocked.CompareExchange(ref _scheduledOrRunning, 1, 0) != 0)
        {
            return true;
        }

        if (_schedule(Run))
        {
            return true;
        }

        Volatile.Write(ref _scheduledOrRunning, 0);
        return false;
    }

    private void Run()
    {
        Interlocked.Exchange(ref _requested, 0);
        try
        {
            _action();
        }
        finally
        {
            Volatile.Write(ref _scheduledOrRunning, 0);
            if (Interlocked.Exchange(ref _requested, 0) != 0)
            {
                Request();
            }
        }
    }
}
