using Microsoft.UI.Dispatching;
using MiaDock.Core.Presentation;

namespace MiaDock.App.Services;

public sealed class IslandAutoCollapseController : IDisposable
{
    private readonly DispatcherQueueTimer _pointerExitTimer;
    private readonly DispatcherQueueTimer _notificationTimer;
    private readonly DispatcherQueueTimer _inactivityTimer;
    private readonly Dictionary<DispatcherQueueTimer, IslandTrigger> _timerTriggers = new();
    private bool _disposed;

    public IslandAutoCollapseController(DispatcherQueue dispatcherQueue, IslandMotionOptions options)
    {
        ArgumentNullException.ThrowIfNull(dispatcherQueue);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        _pointerExitTimer = CreateTimer(dispatcherQueue, options.PointerExitDelay, IslandTrigger.PointerExited);
        _notificationTimer = CreateTimer(
            dispatcherQueue,
            options.NotificationVisibleDuration,
            IslandTrigger.NotificationElapsed);
        _inactivityTimer = CreateTimer(
            dispatcherQueue,
            options.ExpandedInactivityDuration,
            IslandTrigger.InactivityElapsed);
    }

    public event EventHandler<IslandTrigger>? Elapsed;

    public void UpdateOptions(IslandMotionOptions options)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        _pointerExitTimer.Interval = options.PointerExitDelay;
        _notificationTimer.Interval = options.NotificationVisibleDuration;
        _inactivityTimer.Interval = options.ExpandedInactivityDuration;
    }

    public void SetNotificationDuration(TimeSpan duration)
    {
        ThrowIfDisposed();
        if (duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        _notificationTimer.Interval = duration;
    }

    public void PointerEntered()
    {
        ThrowIfDisposed();
        _pointerExitTimer.Stop();
        RestartIfRunning(_inactivityTimer);
    }

    public void PointerExited()
    {
        ThrowIfDisposed();
        Restart(_pointerExitTimer);
    }

    public void RegisterActivity(IslandVisualState state)
    {
        ThrowIfDisposed();
        if (state == IslandVisualState.ExpandedModule)
        {
            Restart(_inactivityTimer);
        }
    }

    public void ObserveTransition(IslandTransition transition)
    {
        ThrowIfDisposed();

        if (transition.CurrentState == IslandVisualState.ModuleNotification)
        {
            if (transition.Trigger == IslandTrigger.ModuleEventReceived || transition.Changed)
            {
                Restart(_notificationTimer);
            }
        }
        else
        {
            _notificationTimer.Stop();
        }

        if (transition.CurrentState == IslandVisualState.ExpandedModule)
        {
            Restart(_inactivityTimer);
        }
        else
        {
            _inactivityTimer.Stop();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeTimer(_pointerExitTimer);
        DisposeTimer(_notificationTimer);
        DisposeTimer(_inactivityTimer);
    }

    private DispatcherQueueTimer CreateTimer(
        DispatcherQueue dispatcherQueue,
        TimeSpan interval,
        IslandTrigger trigger)
    {
        var timer = dispatcherQueue.CreateTimer();
        timer.Interval = interval;
        timer.IsRepeating = false;
        timer.Tick += OnTimerTick;
        _timerTriggers[timer] = trigger;
        return timer;
    }

    private void OnTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        if (_timerTriggers.TryGetValue(sender, out var trigger))
        {
            Elapsed?.Invoke(this, trigger);
        }
    }

    private static void Restart(DispatcherQueueTimer timer)
    {
        timer.Stop();
        timer.Start();
    }

    private static void RestartIfRunning(DispatcherQueueTimer timer)
    {
        if (timer.IsRunning)
        {
            Restart(timer);
        }
    }

    private void DisposeTimer(DispatcherQueueTimer timer)
    {
        timer.Stop();
        timer.Tick -= OnTimerTick;
        _timerTriggers.Remove(timer);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
