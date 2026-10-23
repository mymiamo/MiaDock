using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;

namespace MiaDock.App.Animations;

public sealed class IslandBoundsAnimator : IDisposable
{
    private const double MinimumFrameDelta = 0.5;
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private Session? _session;
    private sealed class Session
    {
        public EventHandler<object>? Handler;
        public readonly TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationTokenRegistration Registration;
    }

    public bool IsRunning => _session is not null;

    public Task AnimateAsync(
        IslandVisualMetrics from,
        IslandVisualMetrics to,
        TimeSpan duration,
        Action<IslandVisualMetrics> apply,
        CancellationToken cancellationToken) =>
        AnimateAsync(from, to, duration, apply, BoundsEasingProfile.Cubic, cancellationToken);

    public Task AnimateAsync(
        IslandVisualMetrics from,
        IslandVisualMetrics to,
        TimeSpan duration,
        Action<IslandVisualMetrics> apply,
        BoundsEasingProfile easing,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(apply);
        if (!_dispatcher.HasThreadAccess) throw new InvalidOperationException("Bounds animations require the UI thread.");
        Cancel();
        cancellationToken.ThrowIfCancellationRequested();

        if (duration <= TimeSpan.Zero || from == to)
        {
            apply(to);
            return Task.CompletedTask;
        }

        var stopwatch = Stopwatch.StartNew();
        var lastApplied = from;
        var session = new Session();
        _session = session;
        session.Handler = (_, _) =>
        {
            if (_session != session) return;
            try
            {
                var progress = Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
                if (progress >= 1)
                {
                    apply(to);
                    Finish(session, cancelled: false);
                    return;
                }

                var easedProgress = Ease(progress, easing);
                var current = Interpolate(from, to, easedProgress);
                if (HasMeaningfulDifference(lastApplied, current))
                {
                    apply(current);
                    lastApplied = current;
                }
            }
            catch (Exception exception)
            {
                Finish(session, cancelled: false, exception);
            }
        };

        CompositionTarget.Rendering += session.Handler;
        // Always queue: an already-cancelled token can invoke Register inline,
        // before Registration has been assigned. The callback never touches XAML.
        session.Registration = cancellationToken.Register(() =>
            _dispatcher.TryEnqueue(() => Finish(session, cancelled: true)));
        return session.Completion.Task;
    }

    public void Cancel()
    {
        var session = _session;
        if (session is null) return;
        if (_dispatcher.HasThreadAccess) Finish(session, cancelled: true);
        else _dispatcher.TryEnqueue(() => Finish(session, cancelled: true));
    }

    public void Dispose() => Cancel();

    public static IslandVisualMetrics Interpolate(
        IslandVisualMetrics from,
        IslandVisualMetrics to,
        double progress)
    {
        var amount = Math.Clamp(progress, 0, 1);
        return new IslandVisualMetrics(
            Lerp(from.Width, to.Width, amount),
            Lerp(from.Height, to.Height, amount),
            new(
                Lerp(from.CornerRadii.TopLeft, to.CornerRadii.TopLeft, amount),
                Lerp(from.CornerRadii.TopRight, to.CornerRadii.TopRight, amount),
                Lerp(from.CornerRadii.BottomRight, to.CornerRadii.BottomRight, amount),
                Lerp(from.CornerRadii.BottomLeft, to.CornerRadii.BottomLeft, amount)));
    }

    /// <summary>
    /// Soft spring/back ease-out with controlled overshoot.
    /// Springiness 0 ≈ cubic; 1 ≈ mild Dynamic-Island style settle (~5% peak overshoot).
    /// </summary>
    public static double Ease(double progress, BoundsEasingProfile profile)
    {
        var amount = Math.Clamp(progress, 0, 1);
        return profile.Kind switch
        {
            BoundsEasingKind.SoftSpringOut => EaseOutSoftSpring(amount, profile.Springiness),
            _ => EaseOutCubic(amount)
        };
    }

    private static double Lerp(double from, double to, double amount) => from + ((to - from) * amount);

    private static bool HasMeaningfulDifference(IslandVisualMetrics previous, IslandVisualMetrics current) =>
        Math.Abs(previous.Width - current.Width) >= MinimumFrameDelta ||
        Math.Abs(previous.Height - current.Height) >= MinimumFrameDelta ||
        Math.Abs(previous.CornerRadii.TopLeft - current.CornerRadii.TopLeft) >= MinimumFrameDelta ||
        Math.Abs(previous.CornerRadii.TopRight - current.CornerRadii.TopRight) >= MinimumFrameDelta ||
        Math.Abs(previous.CornerRadii.BottomRight - current.CornerRadii.BottomRight) >= MinimumFrameDelta ||
        Math.Abs(previous.CornerRadii.BottomLeft - current.CornerRadii.BottomLeft) >= MinimumFrameDelta;

    private static double EaseOutCubic(double progress) => 1 - Math.Pow(1 - progress, 3);

    private static double EaseOutSoftSpring(double progress, double springiness)
    {
        // BackEaseOut with a capped overshoot coefficient.
        // c1 ranges ~0.55..1.35 so bounce stays subtle even at Springiness=1.
        var c1 = 0.55 + (0.8 * Math.Clamp(springiness, 0, 1));
        var c3 = c1 + 1;
        var t = progress - 1;
        return 1 + (c3 * t * t * t) + (c1 * t * t);
    }

    private void Finish(Session session, bool cancelled, Exception? failure = null)
    {
        // An apply callback may retarget reentrantly. Finish only this session.
        if (session.Handler is not null) CompositionTarget.Rendering -= session.Handler;
        session.Handler = null;
        session.Registration.Dispose();
        if (_session == session) _session = null;
        if (failure is not null) session.Completion.TrySetException(failure);
        else if (cancelled) session.Completion.TrySetCanceled();
        else session.Completion.TrySetResult();
    }
}
