using System.Runtime.InteropServices;
using MiaDock.Modules.Media.Models;
using MiaDock.Platform.Windows.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class WindowsSystemSpectrumSourceTests
{
    [TestMethod]
    public void EnsureRunning_ProducesSystemSpectrumFromFakeRecorder()
    {
        var capture = new FakeCapture();
        var factory = new FakeCaptureFactory(capture);
        using var source = new WindowsSystemSpectrumSource(factory, null, () => { });
        source.EnsureRunning(Environment.TickCount64);

        capture.Emit(CreateTone(1000, 48000, AudioSpectrumAnalyzer.WindowSize * 2));

        Assert.IsTrue(source.TryGetSnapshot(Environment.TickCount64, out var snapshot));
        Assert.AreEqual(MediaAudioLevelSource.SystemSpectrum, snapshot.Source);
        Assert.IsGreaterThan(snapshot.Bass, snapshot.Vocal);
        Assert.AreEqual(1, factory.CreateCount);
    }

    [TestMethod]
    public void SilentPackets_AreHealthyAndRemainARealSystemSpectrum()
    {
        var capture = new FakeCapture();
        var factory = new FakeCaptureFactory(capture);
        using var source = new WindowsSystemSpectrumSource(factory, null, () => { });
        source.EnsureRunning(Environment.TickCount64);

        capture.Emit(new float[AudioSpectrumAnalyzer.WindowSize], AudioClientBufferFlags.Silent);
        source.EnsureRunning(Environment.TickCount64);

        Assert.IsTrue(source.TryGetSnapshot(Environment.TickCount64, out var snapshot));
        Assert.AreEqual(MediaAudioLevelSource.SystemSpectrum, snapshot.Source);
        Assert.AreEqual(AudioSpectrumAnalyzer.MinimumScale, snapshot.Bass, 0.0001);
        Assert.AreEqual(1, factory.CreateCount);
    }

    [TestMethod]
    public void UnexpectedStop_UsesOneSecondRetryThenCreatesFreshRecorder()
    {
        var first = new FakeCapture();
        var second = new FakeCapture();
        var factory = new FakeCaptureFactory(first, second);
        using var source = new WindowsSystemSpectrumSource(factory, null, () => { });
        var now = Environment.TickCount64;
        source.EnsureRunning(now);

        first.RaiseStopped(new InvalidOperationException("device invalidated"));
        source.EnsureRunning(now + 1);
        source.EnsureRunning(now + 999);
        Assert.AreEqual(1, factory.CreateCount);
        Assert.AreEqual(1, first.DisposeCount);

        source.EnsureRunning(now + 1001);
        Assert.AreEqual(2, factory.CreateCount);
        Assert.IsTrue(second.Started);
    }

    [TestMethod]
    public void EndpointRestart_DisposesOldRecorderAndIgnoresItsLateCallback()
    {
        var first = new FakeCapture();
        var second = new FakeCapture();
        var factory = new FakeCaptureFactory(first, second);
        using var source = new WindowsSystemSpectrumSource(factory, null, () => { });
        source.EnsureRunning(Environment.TickCount64);
        var lateCallback = first.DataHandlerSnapshot;

        source.RestartForEndpointChange();
        lateCallback?.Invoke(
            MemoryMarshal.AsBytes(CreateTone(1000, 48000, AudioSpectrumAnalyzer.WindowSize).AsSpan()),
            AudioClientBufferFlags.None);
        source.EnsureRunning(Environment.TickCount64);

        Assert.AreEqual(1, first.DisposeCount);
        Assert.AreEqual(2, factory.CreateCount);
        Assert.IsFalse(source.TryGetSnapshot(Environment.TickCount64, out _));
    }

    [TestMethod]
    public void SelectEndpoint_RestartsCaptureWithRequestedRenderDevice()
    {
        var first = new FakeCapture();
        var second = new FakeCapture();
        var factory = new FakeCaptureFactory(first, second);
        using var source = new WindowsSystemSpectrumSource(factory, null, () => { });
        source.EnsureRunning(Environment.TickCount64);

        source.SelectEndpoint("sonar-media");
        source.EnsureRunning(Environment.TickCount64);

        CollectionAssert.AreEqual(
            new string?[] { null, "sonar-media" },
            factory.RequestedEndpointIds);
        Assert.AreEqual(1, first.DisposeCount);
        Assert.IsTrue(second.Started);
    }

    private static float[] CreateTone(int frequency, int sampleRate, int sampleCount)
    {
        var samples = new float[sampleCount];
        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] =
                (float)(0.8 * Math.Sin(2 * Math.PI * frequency * index / sampleRate));
        }

        return samples;
    }

    private sealed class FakeCaptureFactory(params FakeCapture[] captures)
        : ISystemLoopbackCaptureFactory
    {
        private readonly Queue<FakeCapture> _captures = new(captures);
        public int CreateCount { get; private set; }
        public List<string?> RequestedEndpointIds { get; } = [];

        public ISystemLoopbackCapture CreateRenderCapture(string? endpointId)
        {
            CreateCount++;
            RequestedEndpointIds.Add(endpointId);
            return _captures.Dequeue();
        }
    }

    private sealed class FakeCapture : ISystemLoopbackCapture
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
        public string? EndpointId => "fake-endpoint";
        public string? EndpointName => "Fake endpoint";
        public bool Started { get; private set; }
        public int DisposeCount { get; private set; }
        public SystemLoopbackDataHandler? DataHandlerSnapshot => DataAvailable;
        public event SystemLoopbackDataHandler? DataAvailable;
        public event EventHandler<StoppedEventArgs>? RecordingStopped;
        public void Start() => Started = true;
        public void Stop() => Started = false;

        public void Emit(float[] samples, AudioClientBufferFlags flags = AudioClientBufferFlags.None) =>
            DataAvailable?.Invoke(MemoryMarshal.AsBytes(samples.AsSpan()), flags);

        public void RaiseStopped(Exception exception) =>
            RecordingStopped?.Invoke(this, new StoppedEventArgs(exception));

        public void Dispose()
        {
            DisposeCount++;
            Started = false;
        }
    }
}
