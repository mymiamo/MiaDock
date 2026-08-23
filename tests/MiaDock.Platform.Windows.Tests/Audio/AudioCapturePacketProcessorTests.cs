using System.Runtime.InteropServices;
using MiaDock.Modules.Media.Models;
using MiaDock.Platform.Windows.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class AudioCapturePacketProcessorTests
{
    [TestMethod]
    public void TryProcess_SilentFlagIsNotAcceptedAsPcmEvidence()
    {
        var bytes = MemoryMarshal.AsBytes(new float[AudioSpectrumAnalyzer.WindowSize].AsSpan());

        var result = AudioCapturePacketProcessor.TryProcess(
            bytes,
            AudioClientBufferFlags.Silent,
            WaveFormat.CreateIeeeFloatWaveFormat(48000, 1),
            new AudioSpectrumAnalyzer(),
            MediaAudioLevelSource.ProcessSpectrum,
            out _,
            out var maximum);

        Assert.IsFalse(result);
        Assert.AreEqual(0, maximum);
    }

    [TestMethod]
    public void TryProcess_DecodesPcm16WithoutTreatingIntegersAsFloat()
    {
        var samples = new short[AudioSpectrumAnalyzer.WindowSize * 2];
        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = (short)(short.MaxValue * 0.7 *
                Math.Sin(2 * Math.PI * 1000 * index / 48000));
        }

        var result = AudioCapturePacketProcessor.TryProcess(
            MemoryMarshal.AsBytes(samples.AsSpan()),
            AudioClientBufferFlags.None,
            new WaveFormat(48000, 16, 1),
            new AudioSpectrumAnalyzer(),
            MediaAudioLevelSource.EndpointSpectrum,
            out var snapshot,
            out var maximum);

        Assert.IsTrue(result);
        Assert.AreEqual(MediaAudioLevelSource.EndpointSpectrum, snapshot.Source);
        Assert.IsGreaterThan(0.6, maximum);
        Assert.IsGreaterThan(snapshot.Bass, snapshot.Vocal);
        Assert.IsGreaterThan(snapshot.Treble, snapshot.Vocal);
    }
}
