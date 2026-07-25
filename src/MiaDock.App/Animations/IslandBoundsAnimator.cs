using System.Diagnostics;
using Microsoft.UI.Xaml.Media;

namespace MiaDock.App.Animations;

public sealed class IslandBoundsAnimator : IDisposable
{
    private EventHandler<object>? _renderingHandler;
    private TaskCompletionSource? _completion;
    private CancellationTokenRegistration _cancellationRegistration;

    public bool IsRunning => _renderingHandler is not null;

    public Task AnimateAsync(
        IslandVisualMetrics from,
        IslandVisualMetrics to,
        TimeSpan duration,
        Action<IslandVisualMetrics> apply,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(apply);
        Cancel();
        cancellationToken.ThrowIfCancellationRequested();

        if (duration <= TimeSpan.Zero || from == to)
        {
            apply(to);
            return Task.CompletedTask;
        }

        var stopwatch = Stopwatch.StartNew();
        _completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _renderingHandler = (_, _) =>
        {
            var progress = Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
            var easedProgress = EaseOutCubic(progress);
            apply(Interpolate(from, to, easedProgress));

            if (progress >= 1)
            {
                apply(to);
                Complete();
            }
        };

        CompositionTarget.Rendering += _renderingHandler;
        _cancellationRegistration = cancellationToken.Register(
            static state => ((IslandBoundsAnimator)state!).CancelFromToken(),
            this);
        return _completion.Task;
    }

    public void Cancel() => CancelCore(disposeRegistration: true);

    private void CancelFromToken() => CancelCore(disposeRegistration: false);

    private void CancelCore(bool disposeRegistration)
    {
        if (_renderingHandler is not null)
        {
            CompositionTarget.Rendering -= _renderingHandler;
            _renderingHandler = null;
        }

        if (disposeRegistration)
        {
            _cancellationRegistration.Dispose();
            _cancellationRegistration = default;
        }

        _completion?.TrySetCanceled();
        _completion = null;
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
            Lerp(from.CornerRadius, to.CornerRadius, amount));
    }

    private static double Lerp(double from, double to, double amount) => from + ((to - from) * amount);

    private static double EaseOutCubic(double progress) => 1 - Math.Pow(1 - progress, 3);

    private void Complete()
    {
        if (_renderingHandler is not null)
        {
            CompositionTarget.Rendering -= _renderingHandler;
            _renderingHandler = null;
        }

        var registration = _cancellationRegistration;
        _cancellationRegistration = default;
        registration.Dispose();
        _completion?.TrySetResult();
        _completion = null;
    }
}
