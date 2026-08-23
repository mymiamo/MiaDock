using System.Runtime.InteropServices;
using MiaDock.Platform.Windows.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class AudioSpectrumAnalyzerTests
{
    [TestMethod]
    [DataRow(8000, 0)]
    [DataRow(100, 1)]
    [DataRow(1000, 2)]
    public void ProcessFloat32_MapsToneToTrebleBassAndVocalInDisplayOrder(
        int frequency,
        int expectedDominantBand)
    {
        const int sampleRate = 48000;
        var samples = CreateTone(frequency, sampleRate, AudioSpectrumAnalyzer.WindowSize * 2);
        var analyzer = new AudioSpectrumAnalyzer();

        Assert.IsTrue(analyzer.ProcessFloat32(samples, sampleRate, 1, out var snapshot));

        var bands = new[] { snapshot.Treble, snapshot.Bass, snapshot.Vocal };
        Assert.AreEqual(expectedDominantBand, Array.IndexOf(bands, bands.Max()));
        Assert.IsGreaterThan(0.65, bands[expectedDominantBand]);
    }

    [TestMethod]
    public void Reset_ClearsBufferedSamplesAndReturnsUnavailableBaseline()
    {
        var analyzer = new AudioSpectrumAnalyzer();
        var samples = CreateTone(100, 48000, AudioSpectrumAnalyzer.WindowSize * 2);
        Assert.IsTrue(analyzer.ProcessFloat32(samples, 48000, 1, out _));

        var reset = analyzer.Reset();

        Assert.IsFalse(reset.IsAvailable);
        Assert.AreEqual(AudioSpectrumAnalyzer.MinimumScale, reset.Treble, 0.0001);
        Assert.AreEqual(AudioSpectrumAnalyzer.MinimumScale, reset.Bass, 0.0001);
        Assert.AreEqual(AudioSpectrumAnalyzer.MinimumScale, reset.Vocal, 0.0001);
        Assert.IsFalse(analyzer.ProcessFloat32(
            samples.AsSpan(0, AudioSpectrumAnalyzer.WindowSize / 2),
            48000,
            1,
            out _));
    }

    [TestMethod]
    [TestCategory("AudioHardware")]
    public async Task ProcessLoopback_CapturesRealPcmAndProducesVisualActivity()
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable("MIADOCK_RUN_AUDIO_LOOPBACK_TEST"),
            "1",
            StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set MIADOCK_RUN_AUDIO_LOOPBACK_TEST=1 for the real-device check.");
        }

        var analyzer = new AudioSpectrumAnalyzer();
        var activity = new TaskCompletionSource<double>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var capture = await new WasapiRecorderBuilder()
            .WithProcessLoopback(
                (uint)Environment.ProcessId,
                ProcessLoopbackMode.IncludeTargetProcessTree)
            .WithBufferLength(40)
            .BuildAsync();
        CaptureDataAvailableHandler handler = (buffer, _, _, _) =>
        {
            if (capture.WaveFormat.BitsPerSample != 32)
            {
                return;
            }

            var byteCount = buffer.Length - buffer.Length % sizeof(float);
            var samples = MemoryMarshal.Cast<byte, float>(buffer[..byteCount]);
            if (analyzer.ProcessFloat32(
                    samples,
                    capture.WaveFormat.SampleRate,
                    capture.WaveFormat.Channels,
                    out var snapshot))
            {
                var maximum = Math.Max(snapshot.Treble, Math.Max(snapshot.Bass, snapshot.Vocal));
                if (maximum > AudioSpectrumAnalyzer.MinimumScale + 0.1)
                {
                    activity.TrySetResult(maximum);
                }
            }
        };
        capture.DataAvailable += handler;

        await using var playback = await new WasapiPlayerBuilder()
            .WithDefaultDeviceStreamRouting()
            .WithLatency(80)
            .BuildAsync();
        using var endpointEnumerator = new MMDeviceEnumerator();
        using var endpoint = endpointEnumerator.GetDefaultAudioEndpoint(
            DataFlow.Render,
            Role.Multimedia);
        var tone = new SignalGenerator(48000, 2)
        {
            Frequency = 1000,
            Gain = 0.04,
            Type = SignalGeneratorType.Sin
        };

        try
        {
            playback.Init(tone.ToWaveProvider());
            capture.StartRecording();
            playback.Play();
            var endpointPeak = 0f;
            var endpointDeadline = Environment.TickCount64 + 5000;
            while (endpointPeak <= 0.001f && Environment.TickCount64 < endpointDeadline)
            {
                endpointPeak = Math.Max(
                    endpointPeak,
                    endpoint.AudioMeterInformation.MasterPeakValue);
                await Task.Delay(20);
            }

            var maximum = await activity.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsGreaterThan(AudioSpectrumAnalyzer.MinimumScale + 0.1, maximum);
            Assert.IsGreaterThan(0.001f, endpointPeak);
        }
        finally
        {
            playback.Stop();
            capture.StopRecording();
            capture.DataAvailable -= handler;
        }
    }

    private static float[] CreateTone(int frequency, int sampleRate, int sampleCount)
    {
        var samples = new float[sampleCount];
        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = (float)(0.8 * Math.Sin(2 * Math.PI * frequency * index / sampleRate));
        }

        return samples;
    }
}
