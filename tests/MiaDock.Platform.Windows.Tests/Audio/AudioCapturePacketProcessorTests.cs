using System.Runtime.InteropServices;
using MiaDock.Platform.Windows.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class AudioCapturePacketProcessorTests
{
    [TestMethod]
    public void TryDecode_SilentPacketProducesZeroFramesWithoutBecomingAnError()
    {
        var bytes = MemoryMarshal.AsBytes(new float[32].AsSpan());
        var destination = Enumerable.Repeat(1f, 32).ToArray();

        var result = AudioCapturePacketProcessor.TryDecode(
            bytes,
            AudioClientBufferFlags.Silent,
            WaveFormat.CreateIeeeFloatWaveFormat(48000, 1),
            [0],
            destination,
            out var frames,
            out var maximum);

        Assert.IsTrue(result);
        Assert.AreEqual(32, frames);
        Assert.AreEqual(0, maximum);
        Assert.IsTrue(destination.All(sample => sample == 0));
    }

    [TestMethod]
    public void TryDecode_FloatExtensibleSelectsEightDistributedChannels()
    {
        const int channels = 16;
        var selected = AudioCapturePacketProcessor.SelectChannelIndices(channels);
        var samples = Enumerable.Range(0, channels).Select(index => index / 16f).ToArray();
        var destination = new float[selected.Length];
        var format = new WaveFormatExtensible(96000, 32, channels, true, 32, 0);

        Assert.IsTrue(AudioCapturePacketProcessor.TryDecode(
            MemoryMarshal.AsBytes(samples.AsSpan()),
            AudioClientBufferFlags.None,
            format,
            selected,
            destination,
            out var frames,
            out _));

        Assert.AreEqual(1, frames);
        Assert.HasCount(AudioSpectrumAnalyzer.MaximumAnalysisChannels, selected);
        Assert.AreEqual(0, selected[0]);
        Assert.AreEqual(channels - 1, selected[^1]);
        Assert.AreEqual(samples[^1], destination[^1], 0.0001);
    }

    [TestMethod]
    public void TryDecode_ConvertsPcmSixteenTwentyFourAndThirtyTwo()
    {
        AssertDecoded([0x00, 0x40], new WaveFormat(48000, 16, 1), 0.5f);
        AssertDecoded([0x00, 0x00, 0x40], new WaveFormat(48000, 24, 1), 0.5f);
        AssertDecoded([0x00, 0x00, 0x00, 0x40],
            new WaveFormatExtensible(48000, 32, 1, false, 32, 0), 0.5f);
    }

    [TestMethod]
    public void TryDecode_ReplacesNonFiniteFloatAndRejectsIncompleteFrame()
    {
        var nonFinite = new[] { float.NaN, float.PositiveInfinity, -0.5f };
        var decoded = new float[nonFinite.Length];
        Assert.IsTrue(AudioCapturePacketProcessor.TryDecode(
            MemoryMarshal.AsBytes(nonFinite.AsSpan()),
            AudioClientBufferFlags.None,
            WaveFormat.CreateIeeeFloatWaveFormat(44100, 1),
            [0],
            decoded,
            out var frames,
            out var maximum));
        CollectionAssert.AreEqual(new[] { 0f, 0f, -0.5f }, decoded);
        Assert.AreEqual(3, frames);
        Assert.AreEqual(0.5, maximum, 0.0001);

        Assert.IsFalse(AudioCapturePacketProcessor.TryDecode(
            [0x01],
            AudioClientBufferFlags.None,
            new WaveFormat(48000, 16, 1),
            [0],
            new float[1],
            out _,
            out _));
    }

    private static void AssertDecoded(byte[] bytes, WaveFormat format, float expected)
    {
        var destination = new float[1];
        Assert.IsTrue(AudioCapturePacketProcessor.TryDecode(
            bytes,
            AudioClientBufferFlags.None,
            format,
            [0],
            destination,
            out var frames,
            out _));
        Assert.AreEqual(1, frames);
        Assert.AreEqual(expected, destination[0], 0.0001);
    }
}
