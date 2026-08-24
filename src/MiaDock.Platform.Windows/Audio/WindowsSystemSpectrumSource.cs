using System.Runtime.InteropServices;
using MiaDock.Core.Logging;
using MiaDock.Modules.Media.Models;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace MiaDock.Platform.Windows.Audio;

/// <summary>
/// Owns one default-render WASAPI loopback recorder. Recorder lifecycle is
/// independent from media-session rebinding; only the meter worker starts,
/// stops, restarts, analyzes, and disposes the source.
/// </summary>
internal sealed class WindowsSystemSpectrumSource : IDisposable
{
    internal const int SnapshotFreshnessMilliseconds = 500;
    internal const int CaptureStallMilliseconds = 2000;
    internal const int AnalysisIntervalMilliseconds = 33;
    private static readonly int[] RetryDelaysMilliseconds = [1000, 2000, 5000];
    private readonly object _gate = new();
    private readonly ISystemLoopbackCaptureFactory _captureFactory;
    private readonly ILogService? _log;
    private readonly Action _wakeWorker;
    private readonly AudioSpectrumAnalyzer _analyzer = new();
    private ISystemLoopbackCapture? _capture;
    private SystemLoopbackDataHandler? _dataHandler;
    private EventHandler<StoppedEventArgs>? _stoppedHandler;
    private float[] _decodeBuffer = [];
    private int[] _selectedChannels = [];
    private long _generation;
    private long _captureStartedAt;
    private long _lastPacketAt;
    private long _lastAnalysisAt;
    private long _latestSnapshotAt;
    private long _nextRetryAt;
    private int _retryIndex;
    private string? _requestedEndpointId;
    private bool _packetObserved;
    private bool _unexpectedStop;
    private Exception? _unexpectedStopException;
    private bool _disposed;
    private MediaAudioLevelSnapshot _latest = MediaAudioLevelSnapshot.Silent;

    public WindowsSystemSpectrumSource(
        ISystemLoopbackCaptureFactory captureFactory,
        ILogService? log,
        Action wakeWorker)
    {
        _captureFactory = captureFactory ?? throw new ArgumentNullException(nameof(captureFactory));
        _log = log;
        _wakeWorker = wakeWorker ?? throw new ArgumentNullException(nameof(wakeWorker));
    }

    public string? EndpointId
    {
        get
        {
            lock (_gate)
            {
                return _capture?.EndpointId;
            }
        }
    }

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _capture is not null && !_unexpectedStop;
            }
        }
    }

    public void SelectEndpoint(string? endpointId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        endpointId = string.IsNullOrWhiteSpace(endpointId) ? null : endpointId.Trim();
        lock (_gate)
        {
            if (string.Equals(_requestedEndpointId, endpointId, StringComparison.Ordinal))
            {
                return;
            }
        }

        StopCore(resetRetry: true);
        lock (_gate)
        {
            _requestedEndpointId = endpointId;
        }
    }

    public void EnsureRunning(long now)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        string? restartReason = null;
        Exception? restartException = null;
        lock (_gate)
        {
            if (_unexpectedStop)
            {
                restartReason = "recording-stopped";
                restartException = _unexpectedStopException;
            }
            else if (_capture is not null &&
                     now - Math.Max(_captureStartedAt, _lastPacketAt) >= CaptureStallMilliseconds)
            {
                restartReason = "packet-timeout";
            }
        }

        if (restartReason is not null)
        {
            StopCore(resetRetry: false);
            ScheduleRetry(now, restartReason, restartException);
        }

        lock (_gate)
        {
            if (_capture is not null || now < _nextRetryAt)
            {
                return;
            }
        }

        TryStart(now);
    }

    public bool TryGetSnapshot(long now, out MediaAudioLevelSnapshot snapshot)
    {
        AnalyzePending(now);
        lock (_gate)
        {
            if (_latest.Source == MediaAudioLevelSource.SystemSpectrum &&
                now - _latestSnapshotAt <= SnapshotFreshnessMilliseconds)
            {
                snapshot = _latest;
                return true;
            }
        }

        snapshot = MediaAudioLevelSnapshot.Silent;
        return false;
    }

    public void RestartForEndpointChange()
    {
        if (!_disposed)
        {
            StopCore(resetRetry: true);
        }
    }

    public void Stop()
    {
        if (!_disposed)
        {
            StopCore(resetRetry: true);
        }
    }

    private void TryStart(long now)
    {
        ISystemLoopbackCapture? capture = null;
        try
        {
            string? requestedEndpointId;
            lock (_gate)
            {
                requestedEndpointId = _requestedEndpointId;
            }

            capture = _captureFactory.CreateRenderCapture(requestedEndpointId);
            if (!AudioCapturePacketProcessor.TryValidateFormat(capture.WaveFormat, out var format))
            {
                throw new NotSupportedException($"Unsupported loopback format: {capture.WaveFormat}");
            }

            var selectedChannels = AudioCapturePacketProcessor.SelectChannelIndices(format.Channels);
            var decodeCapacity = checked(Math.Max(
                AudioSpectrumAnalyzer.WindowSize * selectedChannels.Length,
                format.SampleRate * selectedChannels.Length / 4));
            var decodeBuffer = new float[decodeCapacity];
            long generation;
            SystemLoopbackDataHandler dataHandler;
            EventHandler<StoppedEventArgs> stoppedHandler;
            lock (_gate)
            {
                if (_disposed || _capture is not null)
                {
                    capture.Dispose();
                    return;
                }

                generation = ++_generation;
                dataHandler = (buffer, flags) => OnDataAvailable(capture, generation, buffer, flags);
                stoppedHandler = (_, args) => OnRecordingStopped(capture, generation, args);
                capture.DataAvailable += dataHandler;
                capture.RecordingStopped += stoppedHandler;
                _analyzer.Reset();
                _analyzer.Configure(format.SampleRate, selectedChannels.Length);
                _selectedChannels = selectedChannels;
                _decodeBuffer = decodeBuffer;
                _captureStartedAt = now;
                _lastPacketAt = 0;
                _lastAnalysisAt = 0;
                _latestSnapshotAt = 0;
                _latest = MediaAudioLevelSnapshot.Silent;
                _packetObserved = false;
                _unexpectedStop = false;
                _unexpectedStopException = null;
                _capture = capture;
                _dataHandler = dataHandler;
                _stoppedHandler = stoppedHandler;
            }

            capture.Start();
            _log?.Write(
                TechnicalLogLevel.Information,
                TechnicalEventIds.MediaAudioMeterBinding,
                "MediaAudio",
                "Experimental system spectrum capture started.",
                properties: new Dictionary<string, object?>
                {
                    ["sourceMode"] = MediaAudioLevelSource.SystemSpectrum.ToString(),
                    ["requestedEndpointId"] = requestedEndpointId,
                    ["renderEndpointId"] = capture.EndpointId,
                    ["renderEndpointName"] = capture.EndpointName,
                    ["format"] = capture.WaveFormat.ToString(),
                    ["selectedChannels"] = selectedChannels.Length
                });
        }
        catch (Exception exception) when (
            exception is COMException or InvalidComObjectException or InvalidOperationException or
                NotSupportedException or ObjectDisposedException or ArgumentException)
        {
            var ownsCapture = false;
            lock (_gate)
            {
                ownsCapture = ReferenceEquals(_capture, capture);
            }

            if (ownsCapture)
            {
                StopCore(resetRetry: false);
            }
            else if (capture is not null)
            {
                StopOwnedCapture(capture);
            }

            ScheduleRetry(now, "start-failed", exception);
        }
    }

    private void OnDataAvailable(
        ISystemLoopbackCapture capture,
        long generation,
        ReadOnlySpan<byte> buffer,
        AudioClientBufferFlags flags)
    {
        var wake = false;
        lock (_gate)
        {
            if (_disposed || generation != _generation || !ReferenceEquals(_capture, capture))
            {
                return;
            }

            var now = Environment.TickCount64;
            _lastPacketAt = now;
            _unexpectedStop = false;
            var format = capture.WaveFormat.AsStandardWaveFormat();
            var totalFrames = buffer.Length / Math.Max(format.BlockAlign, 1);
            var selectedCount = _selectedChannels.Length;
            var framesPerChunk = selectedCount == 0 ? 0 : _decodeBuffer.Length / selectedCount;
            var processedFrames = 0;
            var decodedAny = false;
            while (processedFrames < totalFrames && framesPerChunk > 0)
            {
                var chunkFrames = Math.Min(framesPerChunk, totalFrames - processedFrames);
                var byteOffset = processedFrames * format.BlockAlign;
                var byteCount = chunkFrames * format.BlockAlign;
                if (!AudioCapturePacketProcessor.TryDecode(
                        buffer.Slice(byteOffset, byteCount),
                        flags,
                        capture.WaveFormat,
                        _selectedChannels,
                        _decodeBuffer,
                        out var decodedFrames,
                        out _))
                {
                    break;
                }

                _analyzer.WriteInterleaved(
                    _decodeBuffer.AsSpan(0, decodedFrames * selectedCount),
                    decodedFrames,
                    selectedCount);
                processedFrames += chunkFrames;
                decodedAny = true;
            }

            if ((flags & AudioClientBufferFlags.Silent) != 0)
            {
                _latest = SilentSpectrum();
                _latestSnapshotAt = now;
                decodedAny = true;
            }

            if (decodedAny && !_packetObserved)
            {
                _packetObserved = true;
                _retryIndex = 0;
                _nextRetryAt = 0;
                _log?.Write(
                    TechnicalLogLevel.Information,
                    TechnicalEventIds.MediaAudioMeterBinding,
                    "MediaAudio",
                    "Experimental system spectrum packets are available.",
                    properties: new Dictionary<string, object?>
                    {
                        ["renderEndpointId"] = capture.EndpointId,
                        ["format"] = capture.WaveFormat.ToString()
                    });
            }

            wake = decodedAny;
        }

        if (wake)
        {
            WakeWorkerSafely();
        }
    }

    private void AnalyzePending(long now)
    {
        lock (_gate)
        {
            if (_capture is null || now - _lastAnalysisAt < AnalysisIntervalMilliseconds)
            {
                return;
            }

            _lastAnalysisAt = now;
            if (_analyzer.TryAnalyze(out var snapshot))
            {
                _latest = snapshot;
                _latestSnapshotAt = now;
            }
        }
    }

    private void OnRecordingStopped(
        ISystemLoopbackCapture capture,
        long generation,
        StoppedEventArgs args)
    {
        lock (_gate)
        {
            if (_disposed || generation != _generation || !ReferenceEquals(_capture, capture))
            {
                return;
            }

            _unexpectedStop = true;
            _unexpectedStopException = args.Exception;
        }

        WakeWorkerSafely();
    }

    private void ScheduleRetry(long now, string reason, Exception? exception)
    {
        int retryDelay;
        lock (_gate)
        {
            retryDelay = RetryDelaysMilliseconds[Math.Min(_retryIndex, RetryDelaysMilliseconds.Length - 1)];
            _retryIndex = Math.Min(_retryIndex + 1, RetryDelaysMilliseconds.Length - 1);
            _nextRetryAt = now + retryDelay;
            _latest = MediaAudioLevelSnapshot.Silent;
            _latestSnapshotAt = 0;
        }

        _log?.Write(
            TechnicalLogLevel.Warning,
            TechnicalEventIds.MediaAudioMeterFailed,
            "MediaAudio",
            "Experimental system spectrum is unavailable; using SessionPeak fallback.",
            exception,
            new Dictionary<string, object?>
            {
                ["reason"] = reason,
                ["retryDelayMs"] = retryDelay,
                ["fallbackSourceMode"] = MediaAudioLevelSource.SessionPeak.ToString()
            });
    }

    private void StopCore(bool resetRetry)
    {
        ISystemLoopbackCapture? capture;
        SystemLoopbackDataHandler? dataHandler;
        EventHandler<StoppedEventArgs>? stoppedHandler;
        lock (_gate)
        {
            capture = _capture;
            dataHandler = _dataHandler;
            stoppedHandler = _stoppedHandler;
            _generation++;
            _capture = null;
            _dataHandler = null;
            _stoppedHandler = null;
            _selectedChannels = [];
            _decodeBuffer = [];
            _captureStartedAt = 0;
            _lastPacketAt = 0;
            _lastAnalysisAt = 0;
            _latestSnapshotAt = 0;
            _latest = _analyzer.Reset();
            _packetObserved = false;
            _unexpectedStop = false;
            _unexpectedStopException = null;
            if (resetRetry)
            {
                _retryIndex = 0;
                _nextRetryAt = 0;
            }
        }

        if (capture is null)
        {
            return;
        }

        if (dataHandler is not null)
        {
            capture.DataAvailable -= dataHandler;
        }

        if (stoppedHandler is not null)
        {
            capture.RecordingStopped -= stoppedHandler;
        }

        StopOwnedCapture(capture);
    }

    private static void StopOwnedCapture(ISystemLoopbackCapture capture)
    {
        try
        {
            capture.Stop();
        }
        catch (Exception exception) when (
            exception is COMException or InvalidComObjectException or
                InvalidOperationException or ObjectDisposedException)
        {
        }

        try
        {
            capture.Dispose();
        }
        catch (Exception exception) when (
            exception is COMException or InvalidComObjectException or
                InvalidOperationException or ObjectDisposedException)
        {
        }
    }

    private void WakeWorkerSafely()
    {
        try
        {
            _wakeWorker();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private static MediaAudioLevelSnapshot SilentSpectrum() => new(
        MediaAudioLevelSource.SystemSpectrum,
        AudioSpectrumAnalyzer.MinimumScale,
        AudioSpectrumAnalyzer.MinimumScale,
        AudioSpectrumAnalyzer.MinimumScale,
        0,
        0);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopCore(resetRetry: true);
        _disposed = true;
    }
}
