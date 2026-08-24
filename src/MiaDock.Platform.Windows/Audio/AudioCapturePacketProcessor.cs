using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MiaDock.Platform.Windows.Audio;

/// <summary>
/// Validates WASAPI packet metadata and decodes selected channels into a
/// caller-owned interleaved float buffer. Captured audio is never retained or logged.
/// </summary>
internal static class AudioCapturePacketProcessor
{
    internal static bool TryDecode(
        ReadOnlySpan<byte> buffer,
        AudioClientBufferFlags flags,
        WaveFormat waveFormat,
        ReadOnlySpan<int> selectedChannels,
        Span<float> destination,
        out int frameCount,
        out double maximumSample)
    {
        frameCount = 0;
        maximumSample = 0;
        if (!TryValidateFormat(waveFormat, out var format) || selectedChannels.IsEmpty)
        {
            return false;
        }

        foreach (var channel in selectedChannels)
        {
            if (channel < 0 || channel >= format.Channels)
            {
                return false;
            }
        }

        var validByteCount = buffer.Length - buffer.Length % format.BlockAlign;
        frameCount = validByteCount / format.BlockAlign;
        var destinationSamples = checked(frameCount * selectedChannels.Length);
        if (destination.Length < destinationSamples)
        {
            frameCount = 0;
            return false;
        }

        if ((flags & AudioClientBufferFlags.Silent) != 0)
        {
            destination[..destinationSamples].Clear();
            return true;
        }

        if (frameCount == 0)
        {
            return false;
        }

        var bytesPerSample = format.BitsPerSample / 8;
        var output = 0;
        for (var frame = 0; frame < frameCount; frame++)
        {
            var frameOffset = frame * format.BlockAlign;
            foreach (var channel in selectedChannels)
            {
                var offset = frameOffset + (channel * bytesPerSample);
                var sample = DecodeSample(buffer[offset..], format);
                destination[output++] = sample;
                maximumSample = Math.Max(maximumSample, Math.Abs(sample));
            }
        }

        return true;
    }

    internal static bool TryValidateFormat(WaveFormat waveFormat, out WaveFormat format)
    {
        format = waveFormat.AsStandardWaveFormat();
        var validBitsPerSample = waveFormat is WaveFormatExtensible extensible
            ? extensible.ValidBitsPerSample
            : format.BitsPerSample;
        var bytesPerSample = format.BitsPerSample / 8;
        return format.SampleRate is >= 8000 and <= 384000 &&
               format.Channels is >= 1 and <= 32 &&
               format.BlockAlign > 0 &&
               format.BitsPerSample is 16 or 24 or 32 &&
               validBitsPerSample > 0 &&
               validBitsPerSample <= format.BitsPerSample &&
               format.BlockAlign >= format.Channels * bytesPerSample &&
               (format.Encoding == WaveFormatEncoding.Pcm ||
                format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32);
    }

    internal static int[] SelectChannelIndices(int channelCount)
    {
        if (channelCount is < 1 or > 32)
        {
            return [];
        }

        var selectedCount = Math.Min(channelCount, AudioSpectrumAnalyzer.MaximumAnalysisChannels);
        if (selectedCount == channelCount)
        {
            return Enumerable.Range(0, selectedCount).ToArray();
        }

        var result = new int[selectedCount];
        for (var index = 0; index < selectedCount; index++)
        {
            result[index] = (int)Math.Round(
                index * (channelCount - 1d) / (selectedCount - 1),
                MidpointRounding.AwayFromZero);
        }

        return result;
    }

    private static float DecodeSample(ReadOnlySpan<byte> source, WaveFormat format)
    {
        if (format.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            var sample = MemoryMarshal.Read<float>(source);
            return float.IsFinite(sample) ? Math.Clamp(sample, -1, 1) : 0;
        }

        return format.BitsPerSample switch
        {
            16 => (short)(source[0] | source[1] << 8) / 32768f,
            24 => DecodeInt24(source) / 8388608f,
            32 => (source[0] |
                   source[1] << 8 |
                   source[2] << 16 |
                   source[3] << 24) / 2147483648f,
            _ => 0
        };
    }

    private static int DecodeInt24(ReadOnlySpan<byte> source)
    {
        var value = source[0] | source[1] << 8 | source[2] << 16;
        return (value & 0x800000) != 0 ? value | unchecked((int)0xff000000) : value;
    }
}
