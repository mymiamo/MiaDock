using System.Buffers;
using System.Runtime.InteropServices;
using MiaDock.Modules.Media.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MiaDock.Platform.Windows.Audio;

/// <summary>
/// Validates WASAPI packet metadata and converts supported linear PCM formats
/// to normalized floats without retaining or logging the captured audio.
/// </summary>
internal static class AudioCapturePacketProcessor
{
    internal static bool TryProcess(
        ReadOnlySpan<byte> buffer,
        AudioClientBufferFlags flags,
        WaveFormat waveFormat,
        AudioSpectrumAnalyzer analyzer,
        MediaAudioLevelSource source,
        out MediaAudioLevelSnapshot snapshot,
        out double maximumSample)
    {
        snapshot = MediaAudioLevelSnapshot.Silent;
        maximumSample = 0;
        if ((flags & AudioClientBufferFlags.Silent) != 0 ||
            buffer.IsEmpty ||
            !TryValidateFormat(waveFormat, out var format))
        {
            return false;
        }

        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            var byteCount = buffer.Length - buffer.Length % sizeof(float);
            if (byteCount == 0)
            {
                return false;
            }

            var samples = MemoryMarshal.Cast<byte, float>(buffer[..byteCount]);
            maximumSample = FindMaximum(samples);
            return analyzer.ProcessFloat32(
                       samples,
                       format.SampleRate,
                       format.Channels,
                       out snapshot) &&
                   SetSource(ref snapshot, source);
        }

        if (format.Encoding != WaveFormatEncoding.Pcm ||
            format.BitsPerSample is not (16 or 24 or 32))
        {
            return false;
        }

        var bytesPerSample = format.BitsPerSample / 8;
        var sampleCount = buffer.Length / bytesPerSample;
        if (sampleCount < format.Channels)
        {
            return false;
        }

        var rented = ArrayPool<float>.Shared.Rent(sampleCount);
        try
        {
            var samples = rented.AsSpan(0, sampleCount);
            DecodePcm(buffer, samples, format.BitsPerSample);
            maximumSample = FindMaximum(samples);
            return analyzer.ProcessFloat32(
                       samples,
                       format.SampleRate,
                       format.Channels,
                       out snapshot) &&
                   SetSource(ref snapshot, source);
        }
        finally
        {
            ArrayPool<float>.Shared.Return(rented);
        }
    }

    internal static bool TryValidateFormat(WaveFormat waveFormat, out WaveFormat format)
    {
        format = waveFormat.AsStandardWaveFormat();
        return format.SampleRate is >= 8000 and <= 384000 &&
               format.Channels is >= 1 and <= 32 &&
               format.BlockAlign > 0 &&
               format.BitsPerSample > 0 &&
               format.BlockAlign >= format.Channels * format.BitsPerSample / 8 &&
               (format.Encoding == WaveFormatEncoding.IeeeFloat ||
                format.Encoding == WaveFormatEncoding.Pcm);
    }

    private static bool SetSource(
        ref MediaAudioLevelSnapshot snapshot,
        MediaAudioLevelSource source)
    {
        snapshot = snapshot with { Source = source };
        return true;
    }

    private static double FindMaximum(ReadOnlySpan<float> samples)
    {
        var maximum = 0d;
        foreach (var sample in samples)
        {
            if (float.IsFinite(sample))
            {
                maximum = Math.Max(maximum, Math.Abs(sample));
            }
        }

        return maximum;
    }

    private static void DecodePcm(
        ReadOnlySpan<byte> source,
        Span<float> destination,
        int bitsPerSample)
    {
        var bytesPerSample = bitsPerSample / 8;
        for (var index = 0; index < destination.Length; index++)
        {
            var offset = index * bytesPerSample;
            destination[index] = bitsPerSample switch
            {
                16 => (short)(source[offset] | source[offset + 1] << 8) / 32768f,
                24 => DecodeInt24(source[offset..]) / 8388608f,
                32 => (source[offset] |
                       source[offset + 1] << 8 |
                       source[offset + 2] << 16 |
                       source[offset + 3] << 24) / 2147483648f,
                _ => 0
            };
        }
    }

    private static int DecodeInt24(ReadOnlySpan<byte> source)
    {
        var value = source[0] | source[1] << 8 | source[2] << 16;
        return (value & 0x800000) != 0 ? value | unchecked((int)0xff000000) : value;
    }
}
