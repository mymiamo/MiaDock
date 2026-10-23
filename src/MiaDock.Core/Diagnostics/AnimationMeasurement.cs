namespace MiaDock.Core.Diagnostics;

public sealed record TransitionMeasurement(string Kind, double Milliseconds, bool Cancelled);
public sealed record AnimationPerformanceSummary(
    bool IsRunning, double ElapsedSeconds, int FrameIntervals, int DroppedFrameSamples,
    double P95FrameIntervalMilliseconds, double P99FrameIntervalMilliseconds,
    int DelayedFrames, int EstimatedMissedIntervals, int CompletedTransitions, int CancelledTransitions,
    IReadOnlyList<TransitionMeasurement> Transitions);

/// <summary>Bounded, monotonic, opt-in UI callback measurements; these are not GPU presentation timings.</summary>
public sealed class AnimationMeasurement
{
    public const int FrameCapacity = 16384;
    public const int TransitionCapacity = 1024;
    public static readonly TimeSpan Duration = TimeSpan.FromSeconds(60);
    private readonly Queue<double> _frames = new();
    private readonly Queue<TransitionMeasurement> _transitions = new();
    private TimeSpan _started, _lastFrame, _elapsed;
    private bool _hasFrame;
    private int _dropped, _completed, _cancelled, _delayed, _missed;
    public bool IsRunning { get; private set; }
    public long Session { get; private set; }
    public bool HasMeasurement { get; private set; }

    public void Start(TimeSpan now)
    {
        if (IsRunning) return;
        Session++;
        _frames.Clear(); _transitions.Clear();
        _dropped = _completed = _cancelled = _delayed = _missed = 0;
        _started = now; _elapsed = TimeSpan.Zero; _hasFrame = false;
        IsRunning = HasMeasurement = true;
    }

    public void Tick(TimeSpan now)
    {
        if (!IsRunning) return;
        _elapsed = TimeSpan.FromTicks(Math.Clamp((now - _started).Ticks, 0, Duration.Ticks));
        if (now - _started >= Duration) IsRunning = false;
    }

    public void Frame(TimeSpan now)
    {
        Tick(now);
        if (!IsRunning) return;
        if (_hasFrame && now > _lastFrame)
        {
            var milliseconds = (now - _lastFrame).TotalMilliseconds;
            if (_frames.Count == FrameCapacity) { _frames.Dequeue(); _dropped++; }
            _frames.Enqueue(milliseconds);
            // A delayed UI interval exceeds the 60 Hz budget by 50%.
            if (milliseconds > 25) _delayed++;
            _missed += Math.Max(0, (int)Math.Round(milliseconds / (1000d / 60)) - 1);
        }
        _lastFrame = now; _hasFrame = true;
    }

    public void Transition(long session, string kind, TimeSpan started, TimeSpan now, bool cancelled)
    {
        Tick(now);
        if (!IsRunning || session != Session || started < _started || now < started) return;
        if (_transitions.Count == TransitionCapacity) _transitions.Dequeue();
        _transitions.Enqueue(new(kind, (now - started).TotalMilliseconds, cancelled));
        if (cancelled) _cancelled++; else _completed++;
    }

    public void Stop(TimeSpan now) { Tick(now); IsRunning = false; }

    public AnimationPerformanceSummary Snapshot(TimeSpan now)
    {
        Tick(now);
        var sorted = _frames.Order().ToArray();
        double Percentile(double percentile) => sorted.Length == 0 ? 0 : sorted[(int)Math.Ceiling(sorted.Length * percentile) - 1];
        return new(IsRunning, _elapsed.TotalSeconds, sorted.Length, _dropped,
            Percentile(.95), Percentile(.99), _delayed, _missed, _completed, _cancelled, _transitions.ToArray());
    }
}
