using MiaDock.Platform.Windows.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class WindowsSystemSpectrumHardwareTests
{
    [TestMethod]
    [TestCategory("AudioHardware")]
    public async Task DefaultRenderLoopback_CapturesToneAndProducesMidBandActivity()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("MIADOCK_RUN_AUDIO_LOOPBACK_TEST"),
                "1",
                StringComparison.Ordinal))
        {
            Assert.Inconclusive("Set MIADOCK_RUN_AUDIO_LOOPBACK_TEST=1 for the real-device check.");
        }

        var endpointId = Environment.GetEnvironmentVariable(
            "MIADOCK_AUDIO_LOOPBACK_ENDPOINT_ID");
        endpointId = string.IsNullOrWhiteSpace(endpointId) ? null : endpointId.Trim();
        using var source = new WindowsSystemSpectrumSource(
            new WindowsSystemLoopbackCaptureFactory(),
            null,
            () => { });
        source.SelectEndpoint(endpointId);

        using var deviceEnumerator = endpointId is null ? null : new MMDeviceEnumerator();
        using var playbackDevice = deviceEnumerator?.GetDevice(endpointId!);
        var playerBuilder = new WasapiPlayerBuilder().WithLatency(80);
        if (playbackDevice is null)
        {
            playerBuilder.WithDefaultDeviceStreamRouting();
        }
        else
        {
            playerBuilder.WithDevice(playbackDevice).WithSharedMode().WithEventSync();
        }

        await using var playback = await playerBuilder.BuildAsync();
        var tone = new SignalGenerator(48000, 2)
        {
            Frequency = 1000,
            Gain = 0.04,
            Type = SignalGeneratorType.Sin
        };

        try
        {
            source.EnsureRunning(Environment.TickCount64);
            playback.Init(tone.ToWaveProvider());
            playback.Play();
            var deadline = Environment.TickCount64 + 5000;
            var detected = false;
            while (Environment.TickCount64 < deadline)
            {
                var now = Environment.TickCount64;
                source.EnsureRunning(now);
                if (source.TryGetSnapshot(now, out var snapshot) &&
                    snapshot.Vocal > AudioSpectrumAnalyzer.MinimumScale + 0.1)
                {
                    Assert.IsTrue(snapshot.HasFrequencySpectrum);
                    detected = true;
                    break;
                }

                await Task.Delay(33);
            }

            Assert.IsTrue(
                detected,
                $"The render loopback did not produce spectrum activity in five seconds. Endpoint: {endpointId ?? "default"}.");

            var continuityDeadline = Environment.TickCount64 + 2500;
            var observedFrames = 0;
            var spectrumFrames = 0;
            while (Environment.TickCount64 < continuityDeadline)
            {
                var now = Environment.TickCount64;
                source.EnsureRunning(now);
                observedFrames++;
                if (source.TryGetSnapshot(now, out var snapshot) && snapshot.HasFrequencySpectrum)
                {
                    spectrumFrames++;
                }

                await Task.Delay(33);
            }

            Assert.IsTrue(
                spectrumFrames >= observedFrames * 9 / 10,
                $"Expected continuous spectrum frames, but received {spectrumFrames} of {observedFrames} observations.");
        }
        finally
        {
            playback.Stop();
            source.Stop();
        }
    }

}
