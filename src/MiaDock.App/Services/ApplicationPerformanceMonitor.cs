using System.Diagnostics;
using System.ComponentModel;
using MiaDock.Core.Diagnostics;
using MiaDock.Core.Threading;
using MiaDock.Modules.Media.ViewModels;
using MiaDock.Platform.Windows.Diagnostics;
using MiaDock.App.Animations;

namespace MiaDock.App.Services;

public sealed class ApplicationPerformanceMonitor : IAsyncDisposable
{
    private readonly ProcessPerformanceSampler _sampler = new();
    private readonly PerformanceAccumulator _accumulator = new(Environment.ProcessorCount);
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _loop;
    private readonly MusicModuleViewModel _music;
    private readonly IUiDispatcher _dispatcher;
    private readonly AnimationPerformanceMonitor _animation;
    private long _lastSample = Stopwatch.GetTimestamp();
    private double? _dockReadyMilliseconds;
    private bool _disposed;

    public ApplicationPerformanceMonitor(MusicModuleViewModel music, IUiDispatcher dispatcher, AnimationPerformanceMonitor animation)
    {
        _music = music;
        _dispatcher = dispatcher;
        _animation = animation;
        Sample();
        _loop = RunAsync();
    }

    public void MarkDockReady() => _dockReadyMilliseconds ??= Math.Max(0, (DateTime.UtcNow - _sampler.StartedAtUtc).TotalMilliseconds);

    public ApplicationPerformanceSnapshot Snapshot() => _accumulator.Snapshot(_dockReadyMilliseconds) with { Animation = _animation.Snapshot() };
    public void StartAnimationMeasurement() => _animation.Start();
    public void StopAnimationMeasurement() => _animation.Stop();

    private void Sample()
    {
        if (_disposed) return;
        try
        {
            var now = Stopwatch.GetTimestamp();
            var scenario = _music.HasFrequencySpectrum && _music.IsAudioLevelAvailable ? PerformanceScenario.Spectrum
                : _music.IsPlaying ? PerformanceScenario.Media : PerformanceScenario.Idle;
            _accumulator.Add(scenario, _sampler.Read(), Stopwatch.GetElapsedTime(_lastSample, now));
            _lastSample = now;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            // Diagnostics must not interfere with the application's lifetime.
        }
    }

    private async Task RunAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        try
        {
            while (await timer.WaitForNextTickAsync(_cancellation.Token).ConfigureAwait(false))
                _dispatcher.TryEnqueue(Sample);
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _cancellation.Cancel();
        await _loop.ConfigureAwait(false);
        _sampler.Dispose();
        _cancellation.Dispose();
    }
}
