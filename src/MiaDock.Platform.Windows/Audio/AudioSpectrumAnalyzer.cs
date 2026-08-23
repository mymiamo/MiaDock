using System.Numerics;
using MiaDock.Modules.Media.Models;

namespace MiaDock.Platform.Windows.Audio;

/// <summary>
/// Converts PCM samples into the three frequency bands used by the compact
/// media visualizer. Snapshot order is treble, bass, then vocal/mid.
/// </summary>
internal sealed class AudioSpectrumAnalyzer
{
    internal const int WindowSize = 2048;
    internal const double MinimumScale = 0.18;
    private const int HopSize = WindowSize / 2;
    private const double NoiseFloorDecibels = -68;
    private const double VisualCeilingDecibels = -12;
    private readonly float[] _monoWindow = new float[WindowSize];
    private readonly Complex[] _fft = new Complex[WindowSize];
    private int _sampleCount;
    private double _treble = MinimumScale;
    private double _bass = MinimumScale;
    private double _vocal = MinimumScale;

    public bool ProcessFloat32(
        ReadOnlySpan<float> interleavedSamples,
        int sampleRate,
        int channelCount,
        out MediaAudioLevelSnapshot snapshot)
    {
        snapshot = MediaAudioLevelSnapshot.Silent;
        if (sampleRate <= 0 || channelCount <= 0 || interleavedSamples.Length < channelCount)
        {
            return false;
        }

        var produced = false;
        var frameCount = interleavedSamples.Length / channelCount;
        for (var frame = 0; frame < frameCount; frame++)
        {
            double mono = 0;
            var frameOffset = frame * channelCount;
            for (var channel = 0; channel < channelCount; channel++)
            {
                var sample = interleavedSamples[frameOffset + channel];
                mono += float.IsFinite(sample) ? Math.Clamp(sample, -1, 1) : 0;
            }

            _monoWindow[_sampleCount++] = (float)(mono / channelCount);
            if (_sampleCount < WindowSize)
            {
                continue;
            }

            snapshot = AnalyzeWindow(sampleRate);
            produced = true;
            Array.Copy(_monoWindow, WindowSize - HopSize, _monoWindow, 0, HopSize);
            _sampleCount = HopSize;
        }

        return produced;
    }

    public MediaAudioLevelSnapshot Reset()
    {
        Array.Clear(_monoWindow);
        _sampleCount = 0;
        _treble = _bass = _vocal = MinimumScale;
        return MediaAudioLevelSnapshot.Silent;
    }

    private MediaAudioLevelSnapshot AnalyzeWindow(int sampleRate)
    {
        var windowSum = 0d;
        for (var index = 0; index < WindowSize; index++)
        {
            var hann = 0.5 - 0.5 * Math.Cos(2 * Math.PI * index / (WindowSize - 1));
            windowSum += hann;
            _fft[index] = new Complex(_monoWindow[index] * hann, 0);
        }

        TransformInPlace(_fft);
        var frequencyPerBin = (double)sampleRate / WindowSize;
        var nyquist = sampleRate / 2d;
        var bassMagnitude = FindBandPeak(55, 250, frequencyPerBin, windowSum);
        var vocalMagnitude = FindBandPeak(250, Math.Min(4000, nyquist), frequencyPerBin, windowSum);
        var trebleMagnitude = FindBandPeak(4000, Math.Min(16000, nyquist), frequencyPerBin, windowSum);

        _treble = Follow(_treble, Shape(trebleMagnitude));
        _bass = Follow(_bass, Shape(bassMagnitude));
        _vocal = Follow(_vocal, Shape(vocalMagnitude));
        return new MediaAudioLevelSnapshot(
            MediaAudioLevelSource.ProcessSpectrum,
            _treble,
            _bass,
            _vocal,
            0,
            0);
    }

    private double FindBandPeak(
        double minimumFrequency,
        double maximumFrequency,
        double frequencyPerBin,
        double windowSum)
    {
        if (maximumFrequency <= minimumFrequency || frequencyPerBin <= 0)
        {
            return 0;
        }

        var firstBin = Math.Max(1, (int)Math.Ceiling(minimumFrequency / frequencyPerBin));
        var lastBin = Math.Min(WindowSize / 2, (int)Math.Floor(maximumFrequency / frequencyPerBin));
        var peak = 0d;
        for (var bin = firstBin; bin <= lastBin; bin++)
        {
            peak = Math.Max(peak, _fft[bin].Magnitude * 2 / windowSum);
        }

        return peak;
    }

    private static double Shape(double magnitude)
    {
        if (!double.IsFinite(magnitude) || magnitude <= 0)
        {
            return MinimumScale;
        }

        var decibels = 20 * Math.Log10(magnitude);
        var normalized = Math.Clamp(
            (decibels - NoiseFloorDecibels) /
            (VisualCeilingDecibels - NoiseFloorDecibels),
            0,
            1);
        return MinimumScale + Math.Pow(normalized, 0.72) * (1 - MinimumScale);
    }

    private static double Follow(double current, double target)
    {
        var response = target >= current ? 0.78 : 0.24;
        return current + (target - current) * response;
    }

    private static void TransformInPlace(Complex[] buffer)
    {
        for (int source = 1, target = 0; source < buffer.Length; source++)
        {
            var bit = buffer.Length >> 1;
            for (; (target & bit) != 0; bit >>= 1)
            {
                target ^= bit;
            }

            target ^= bit;
            if (source < target)
            {
                (buffer[source], buffer[target]) = (buffer[target], buffer[source]);
            }
        }

        for (var length = 2; length <= buffer.Length; length <<= 1)
        {
            var phaseStep = -2 * Math.PI / length;
            var root = new Complex(Math.Cos(phaseStep), Math.Sin(phaseStep));
            for (var start = 0; start < buffer.Length; start += length)
            {
                var phase = Complex.One;
                var half = length >> 1;
                for (var offset = 0; offset < half; offset++)
                {
                    var even = buffer[start + offset];
                    var odd = buffer[start + offset + half] * phase;
                    buffer[start + offset] = even + odd;
                    buffer[start + offset + half] = even - odd;
                    phase *= root;
                }
            }
        }
    }
}
