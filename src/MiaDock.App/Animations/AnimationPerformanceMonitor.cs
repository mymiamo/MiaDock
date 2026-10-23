using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using MiaDock.Core.Diagnostics;

namespace MiaDock.App.Animations;

/// <summary>Owned by the UI thread. No rendering subscription exists outside an explicitly started session.</summary>
public sealed class AnimationPerformanceMonitor : IAsyncDisposable
{
    private readonly AnimationMeasurement _measurement = new();
    private readonly DispatcherQueue _dispatcher = DispatcherQueue.GetForCurrentThread();
    private DispatcherQueueTimer? _timer;
    private bool _subscribed, _disposed;
    private static TimeSpan Now => TimeSpan.FromSeconds(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
    public AnimationPerformanceSummary? Snapshot()
    {
        VerifyThread();
        if (!_measurement.HasMeasurement) return null;
        var result = _measurement.Snapshot(Now);
        if (!result.IsRunning) Detach();
        return result;
    }

    public void Start()
    {
        VerifyThread();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_measurement.IsRunning) return;
        _measurement.Start(Now);
        CompositionTarget.Rendering += OnRendering;
        _subscribed = true;
        _timer ??= _dispatcher.CreateTimer();
        _timer.Interval = AnimationMeasurement.Duration;
        _timer.IsRepeating = false;
        _timer.Tick -= OnDeadline;
        _timer.Tick += OnDeadline;
        _timer.Start();
    }

    public void Stop() { VerifyThread(); _measurement.Stop(Now); Detach(); }
    public (long Session, TimeSpan Started) BeginTransition() => (_measurement.Session, Now);
    public void EndTransition((long Session, TimeSpan Started) token, string kind, bool cancelled) =>
        _measurement.Transition(token.Session, kind, token.Started, Now, cancelled);
    private void OnRendering(object? sender, object args)
    {
        _measurement.Frame(Now);
        if (!_measurement.IsRunning) Detach();
    }
    private void OnDeadline(DispatcherQueueTimer sender, object args) => Stop();
    private void Detach()
    {
        if (_subscribed) { CompositionTarget.Rendering -= OnRendering; _subscribed = false; }
        _timer?.Stop();
    }
    private void VerifyThread()
    {
        if (!_dispatcher.HasThreadAccess) throw new InvalidOperationException("Animation diagnostics require the UI thread.");
    }
    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        if (_dispatcher.HasThreadAccess) { DisposeOnUiThread(); return ValueTask.CompletedTask; }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_dispatcher.TryEnqueue(() => { DisposeOnUiThread(); completion.TrySetResult(); }))
            completion.TrySetException(new InvalidOperationException("UI dispatcher stopped before animation diagnostics disposal."));
        return new ValueTask(completion.Task);
    }
    private void DisposeOnUiThread()
    {
        if (_disposed) return;
        Stop();
        if (_timer is not null) _timer.Tick -= OnDeadline;
        _timer = null; _disposed = true;
    }
}
