using MiaDock.Modules.Media.Models;
using MiaDock.Platform.Windows.Audio;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class AudioSpectrumAnalyzerTests
{
    [TestMethod]
    [DataRow(8000, 0)]
    [DataRow(100, 1)]
    [DataRow(1000, 2)]
    public void ProcessFloat32_MapsToneToExpectedDisplayBand(
        int frequency,
        int expectedDominantBand)
    {
        const int sampleRate = 48000;
        var analyzer = new AudioSpectrumAnalyzer();

        Assert.IsTrue(analyzer.ProcessFloat32(
            CreateTone(frequency, sampleRate, AudioSpectrumAnalyzer.WindowSize * 2),
            sampleRate,
            1,
            out var snapshot));

        var bands = new[] { snapshot.Treble, snapshot.Bass, snapshot.Vocal };
        Assert.AreEqual(expectedDominantBand, Array.IndexOf(bands, bands.Max()));
        Assert.IsGreaterThan(0.65, bands[expectedDominantBand]);
        Assert.AreEqual(MediaAudioLevelSource.SystemSpectrum, snapshot.Source);
        Assert.IsTrue(snapshot.HasFrequencySpectrum);
    }

    [TestMethod]
    public void ProcessFloat32_SilenceRemainsAtMinimumBands()
    {
        var analyzer = new AudioSpectrumAnalyzer();

        Assert.IsTrue(analyzer.ProcessFloat32(
            new float[AudioSpectrumAnalyzer.WindowSize * 2],
            48000,
            1,
            out var snapshot));

        Assert.AreEqual(AudioSpectrumAnalyzer.MinimumScale, snapshot.Treble, 0.0001);
        Assert.AreEqual(AudioSpectrumAnalyzer.MinimumScale, snapshot.Bass, 0.0001);
        Assert.AreEqual(AudioSpectrumAnalyzer.MinimumScale, snapshot.Vocal, 0.0001);
    }

    [TestMethod]
    public void ProcessFloat32_OppositePhaseStereoDoesNotCancel()
    {
        const int sampleRate = 48000;
        var samples = new float[AudioSpectrumAnalyzer.WindowSize * 2 * 2];
        for (var frame = 0; frame < samples.Length / 2; frame++)
        {
            var sample = (float)(0.8 * Math.Sin(2 * Math.PI * 1000 * frame / sampleRate));
            samples[frame * 2] = sample;
            samples[(frame * 2) + 1] = -sample;
        }

        var analyzer = new AudioSpectrumAnalyzer();
        Assert.IsTrue(analyzer.ProcessFloat32(samples, sampleRate, 2, out var snapshot));
        Assert.IsGreaterThan(0.65, snapshot.Vocal);
    }

    [TestMethod]
    public void ProcessFloat32_AnalyzesAllEightChannelsAtNinetySixKilohertz()
    {
        const int sampleRate = 96000;
        const int channels = 8;
        var frames = AudioSpectrumAnalyzer.WindowSize * 2;
        var samples = new float[frames * channels];
        for (var frame = 0; frame < frames; frame++)
        {
            samples[(frame * channels) + 7] =
                (float)(0.8 * Math.Sin(2 * Math.PI * 8000 * frame / sampleRate));
        }

        var analyzer = new AudioSpectrumAnalyzer();
        Assert.IsTrue(analyzer.ProcessFloat32(samples, sampleRate, channels, out var snapshot));
        Assert.IsGreaterThan(snapshot.Vocal, snapshot.Treble);
        Assert.IsGreaterThan(snapshot.Bass, snapshot.Treble);
    }

    [TestMethod]
    public void Reset_DropsBufferedFrames()
    {
        var analyzer = new AudioSpectrumAnalyzer();
        var samples = CreateTone(100, 48000, AudioSpectrumAnalyzer.WindowSize * 2);
        Assert.IsTrue(analyzer.ProcessFloat32(samples, 48000, 1, out _));

        Assert.IsFalse(analyzer.Reset().IsAvailable);
        Assert.IsFalse(analyzer.ProcessFloat32(
            samples.AsSpan(0, AudioSpectrumAnalyzer.WindowSize / 2),
            48000,
            1,
            out _));
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
}
