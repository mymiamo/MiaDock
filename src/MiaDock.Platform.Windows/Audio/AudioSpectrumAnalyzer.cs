using MiaDock.Modules.Media.Models;
using NAudio.Dsp;

namespace MiaDock.Platform.Windows.Audio;

/// <summary>
/// Buffers up to eight independent channels and converts the latest PCM window
/// into the three frequency bands consumed by the compact media visualizer.
/// FFT work is performed only by the owning meter worker.
/// </summary>
internal sealed class AudioSpectrumAnalyzer
{
    internal const int WindowSize = 2048;
    internal const int HopSize = WindowSize / 2;
    internal const int MaximumAnalysisChannels = 8;
    internal const double MinimumScale = 0.18;
    private const double NoiseFloorDecibels = -68;
    private const double VisualCeilingDecibels = -12;
    private readonly FftProcessor _fft = new(WindowSize, FftWindowType.Hann);
    private readonly float[][] _channelRings = CreateChannelBuffers();
    private readonly float[] _analysisWindow = new float[WindowSize];
    private readonly Complex[] _spectrum = new Complex[(WindowSize / 2) + 1];
    private long _framesWritten;
    private long _lastAnalyzedFrame;
    private int _writeIndex;
    private int _sampleRate;
    private int _channelCount;
    private double _treble = MinimumScale;
    private double _bass = MinimumScale;
    private double _vocal = MinimumScale;

    public void Configure(int sampleRate, int channelCount)
    {
        if (sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleRate));
        }

        if (channelCount is < 1 or > MaximumAnalysisChannels)
        {
            throw new ArgumentOutOfRangeException(nameof(channelCount));
        }

        if (_sampleRate == sampleRate && _channelCount == channelCount)
        {
            return;
        }

        ResetCore();
        _sampleRate = sampleRate;
        _channelCount = channelCount;
    }

    public void WriteInterleaved(ReadOnlySpan<float> samples, int frameCount, int channelCount)
    {
        if (frameCount <= 0 || channelCount != _channelCount ||
            samples.Length < checked(frameCount * channelCount))
        {
            return;
        }

        for (var frame = 0; frame < frameCount; frame++)
        {
            var frameOffset = frame * channelCount;
            for (var channel = 0; channel < channelCount; channel++)
            {
                var sample = samples[frameOffset + channel];
                _channelRings[channel][_writeIndex] =
                    float.IsFinite(sample) ? Math.Clamp(sample, -1, 1) : 0;
            }

            _writeIndex = (_writeIndex + 1) % WindowSize;
            _framesWritten++;
        }
    }

    public bool TryAnalyze(out MediaAudioLevelSnapshot snapshot)
    {
        snapshot = MediaAudioLevelSnapshot.Silent;
        if (_sampleRate <= 0 || _channelCount <= 0 ||
            _framesWritten < WindowSize ||
            _framesWritten - _lastAnalyzedFrame < HopSize)
        {
            return false;
        }

        var frequencyPerBin = (double)_sampleRate / WindowSize;
        var nyquist = _sampleRate / 2d;
        var bassMagnitude = 0d;
        var vocalMagnitude = 0d;
        var trebleMagnitude = 0d;

        for (var channel = 0; channel < _channelCount; channel++)
        {
            CopyLatestWindow(_channelRings[channel], _analysisWindow);
            _fft.RealForward(_analysisWindow, _spectrum);
            bassMagnitude = Math.Max(bassMagnitude, FindBandPeak(55, 250, frequencyPerBin));
            vocalMagnitude = Math.Max(vocalMagnitude, FindBandPeak(250, Math.Min(4000, nyquist), frequencyPerBin));
            trebleMagnitude = Math.Max(trebleMagnitude, FindBandPeak(4000, Math.Min(16000, nyquist), frequencyPerBin));
        }

        _lastAnalyzedFrame = _framesWritten;
        _treble = Follow(_treble, Shape(trebleMagnitude));
        _bass = Follow(_bass, Shape(bassMagnitude));
        _vocal = Follow(_vocal, Shape(vocalMagnitude));
        snapshot = new MediaAudioLevelSnapshot(
            MediaAudioLevelSource.SystemSpectrum,
            _treble,
            _bass,
            _vocal,
            0,
            0);
        return true;
    }

    // A convenient behavioral test seam for already-decoded PCM.
    public bool ProcessFloat32(
        ReadOnlySpan<float> interleavedSamples,
        int sampleRate,
        int channelCount,
        out MediaAudioLevelSnapshot snapshot)
    {
        var selectedChannels = Math.Min(channelCount, MaximumAnalysisChannels);
        if (sampleRate <= 0 || selectedChannels <= 0 ||
            interleavedSamples.Length < channelCount)
        {
            snapshot = MediaAudioLevelSnapshot.Silent;
            return false;
        }

        Configure(sampleRate, selectedChannels);
        if (selectedChannels == channelCount)
        {
            WriteInterleaved(
                interleavedSamples,
                interleavedSamples.Length / channelCount,
                selectedChannels);
        }
        else
        {
            var frames = interleavedSamples.Length / channelCount;
            var selected = AudioCapturePacketProcessor.SelectChannelIndices(channelCount);
            var scratch = new float[checked(frames * selected.Length)];
            for (var frame = 0; frame < frames; frame++)
            {
                for (var channel = 0; channel < selected.Length; channel++)
                {
                    scratch[(frame * selected.Length) + channel] =
                        interleavedSamples[(frame * channelCount) + selected[channel]];
                }
            }

            WriteInterleaved(scratch, frames, selected.Length);
        }

        return TryAnalyze(out snapshot);
    }

    public MediaAudioLevelSnapshot Reset()
    {
        ResetCore();
        _sampleRate = 0;
        _channelCount = 0;
        return MediaAudioLevelSnapshot.Silent;
    }

    private void ResetCore()
    {
        foreach (var channel in _channelRings)
        {
            Array.Clear(channel);
        }

        _framesWritten = 0;
        _lastAnalyzedFrame = 0;
        _writeIndex = 0;
        _treble = _bass = _vocal = MinimumScale;
    }

    private void CopyLatestWindow(float[] ring, float[] destination)
    {
        var tail = WindowSize - _writeIndex;
        ring.AsSpan(_writeIndex, tail).CopyTo(destination);
        ring.AsSpan(0, _writeIndex).CopyTo(destination.AsSpan(tail));
    }

    private double FindBandPeak(double minimumFrequency, double maximumFrequency, double frequencyPerBin)
    {
        if (maximumFrequency <= minimumFrequency || frequencyPerBin <= 0)
        {
            return 0;
        }

        var firstBin = Math.Max(1, (int)Math.Ceiling(minimumFrequency / frequencyPerBin));
        var lastBin = Math.Min(_spectrum.Length - 1, (int)Math.Floor(maximumFrequency / frequencyPerBin));
        var peak = 0d;
        for (var bin = firstBin; bin <= lastBin; bin++)
        {
            var value = _spectrum[bin];
            peak = Math.Max(
                peak,
                Math.Sqrt((value.Real * value.Real) + (value.Imaginary * value.Imaginary)) * 2);
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
        var response = target >= current ? 0.86 : 0.32;
        return current + (target - current) * response;
    }

    private static float[][] CreateChannelBuffers()
    {
        var result = new float[MaximumAnalysisChannels][];
        for (var index = 0; index < result.Length; index++)
        {
            result[index] = new float[WindowSize];
        }

        return result;
    }
}
