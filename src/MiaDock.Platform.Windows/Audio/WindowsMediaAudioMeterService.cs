using System.Diagnostics;
using System.Runtime.InteropServices;
using MiaDock.Modules.Media.Models;
using MiaDock.Modules.Media.Services;
using MiaDock.Core.Logging;
using NAudio.Wave;

namespace MiaDock.Platform.Windows.Audio;

public sealed class WindowsMediaAudioMeterService : IMediaAudioMeterService
{
    private const int SampleIntervalMilliseconds = 33;
    private readonly IMediaSessionService _media;
    private readonly ILogService? _log;
    private readonly object _gate = new();
    private readonly object _spectrumGate = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly AudioLevelSmoother _smoother = new();
    private readonly AudioSpectrumAnalyzer _spectrumAnalyzer = new();
    private Thread? _worker;
    private bool _active;
    private volatile bool _needsRebind = true;
    private int _diagnosticCheckpointPending = 1;
    private bool _disposed;
    private IMMDeviceEnumerator? _deviceEnumerator;
    private IMMNotificationClient? _deviceNotification;
    private IMMDevice? _renderDevice;
    private IAudioMeterInformation? _endpointMeter;
    private float[] _endpointChannelBuffer = [];
    private IAudioSessionManager2? _sessionManager;
    private readonly List<IAudioSessionControl> _meterControls = [];
    private readonly List<IAudioMeterInformation> _meters = [];
    private readonly List<float[]> _channelBuffers = [];
    private readonly List<uint> _meterProcessIds = [];
    private MediaAudioBindingIdentity _bindingIdentity;
    private WasapiRecorder? _loopbackCapture;
    private CaptureDataAvailableHandler? _loopbackDataHandler;
    private uint _loopbackProcessId;
    private MediaAudioLevelSnapshot _latestSpectrum = MediaAudioLevelSnapshot.Silent;
    private long _latestSpectrumTimestamp;
    private bool _spectrumDataObserved;
    private bool _spectrumVisualActivityObserved;
    private int _spectrumProbeBufferCount;
    private double _maximumObservedSample;
    private bool _sessionPeakFallbackObserved;

    public WindowsMediaAudioMeterService(IMediaSessionService media, ILogService? log = null)
    {
        _media = media;
        _log = log;
        _bindingIdentity = MediaAudioBindingIdentity.From(media.Current);
        _media.SnapshotChanged += OnMediaSnapshotChanged;
        _media.StateChanged += OnMediaStateChanged;
    }

    public MediaAudioLevelSnapshot Current { get; private set; } = MediaAudioLevelSnapshot.Silent;

    public event EventHandler<MediaAudioLevelSnapshot>? LevelChanged;

    public void SetActive(bool active)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _active = active;
            if (active && _worker is null)
            {
                _worker = new Thread(WorkerMain)
                {
                    IsBackground = true,
                    Name = "MiaDock.MediaMeter"
                };
                _worker.SetApartmentState(ApartmentState.MTA);
                _worker.Start();
            }

            _wake.Set();
        }

        if (!active)
        {
            Publish(_smoother.Reset());
        }
    }

    private void WorkerMain()
    {
        var comInitialized = CoreAudioNative.CoInitializeEx(0, CoreAudioNative.CoinitMultithreaded) >= 0;
        try
        {
            InitializeDeviceNotifications();
            while (true)
            {
                bool active;
                bool disposed;
                lock (_gate)
                {
                    active = _active;
                    disposed = _disposed;
                }

                if (disposed)
                {
                    return;
                }

                if (!active ||
                    !_media.Current.HasMedia ||
                    _media.Current.PlaybackStatus != PlaybackStatus.Playing)
                {
                    StopSpectrumCapture();
                    Publish(_smoother.Reset());
                    _wake.WaitOne();
                    continue;
                }

                if (_needsRebind || _meters.Count == 0)
                {
                    var diagnosticCheckpoint =
                        Interlocked.Exchange(ref _diagnosticCheckpointPending, 0) != 0;
                    if (diagnosticCheckpoint)
                    {
                        LogBindingCheckpoint();
                    }

                    BindMeter(diagnosticCheckpoint);
                }

                if (TryReadPeaks(out var leftPeak, out var rightPeak, out var activeProcessId))
                {
                    EnsureSpectrumCapture(activeProcessId);
                    // The session meter is used only to choose the active media process.
                    // Process loopback already contains isolated PCM for that process, so
                    // scaling its FFT with a second driver-provided peak can incorrectly
                    // flatten valid spectrum data to the minimum level.
                    Publish(ReadSpectrumOrSessionPeak(leftPeak, rightPeak));
                    _wake.WaitOne(TimeSpan.FromMilliseconds(SampleIntervalMilliseconds));
                }
                else
                {
                    _needsRebind = true;
                    StopSpectrumCapture();
                    Publish(_smoother.Reset());
                    _wake.WaitOne(TimeSpan.FromSeconds(2));
                }
            }
        }
        catch (Exception exception)
        {
            if (!_disposed)
            {
                Publish(_smoother.Reset());
                _log?.Write(
                    TechnicalLogLevel.Warning,
                    TechnicalEventIds.MediaAudioMeterFailed,
                    "MediaAudio",
                    "Media audio meter worker stopped safely.",
                    exception);
            }
        }
        finally
        {
            TryCleanupAudio();
            if (comInitialized)
            {
                CoreAudioNative.CoUninitialize();
            }

            lock (_gate)
            {
                _worker = null;
            }
        }
    }

    private void InitializeDeviceNotifications()
    {
        _deviceEnumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
        _deviceNotification = new DeviceNotificationCallback(() => !_disposed, RequestRebind);
        CoreAudioNative.ThrowIfFailed(_deviceEnumerator.RegisterEndpointNotificationCallback(_deviceNotification));
    }

    private void BindMeter(bool logResult)
    {
        CleanupBinding();
        _needsRebind = false;
        var sourceId = _media.Current.Source.Id;
        if (string.IsNullOrWhiteSpace(sourceId) || _deviceEnumerator is null)
        {
            if (logResult)
            {
                LogBindingResult("no-media-source", 0, null);
            }
            return;
        }

        try
        {
            CoreAudioNative.ThrowIfFailed(_deviceEnumerator.GetDefaultAudioEndpoint(
                AudioDataFlow.Render, AudioDeviceRole.Multimedia, out _renderDevice!));
            _endpointMeter = Activate<IAudioMeterInformation>(
                _renderDevice,
                CoreAudioNative.MeterInformationId);
            _endpointChannelBuffer = _endpointMeter.GetMeteringChannelCount(out var endpointChannels) >= 0 &&
                                     endpointChannels > 0 && endpointChannels <= 32
                ? new float[endpointChannels]
                : [];
            _sessionManager = Activate<IAudioSessionManager2>(_renderDevice, CoreAudioNative.SessionManagerId);
            CoreAudioNative.ThrowIfFailed(_sessionManager.GetSessionEnumerator(out var sessions));
            try
            {
                CoreAudioNative.ThrowIfFailed(sessions.GetCount(out var count));
                for (var index = 0; index < count; index++)
                {
                    IAudioSessionControl? control = null;
                    try
                    {
                        CoreAudioNative.ThrowIfFailed(sessions.GetSession(index, out control));
                        var control2 = (IAudioSessionControl2)control;
                        CoreAudioNative.ThrowIfFailed(control2.GetProcessId(out var processId));
                        var processName = ResolveProcessName(processId);
                        var processPath = ResolveProcessPath(processId);
                        if (!MediaAudioSessionMatcher.IsMatch(sourceId, processName, processPath))
                        {
                            control = null;
                            continue;
                        }

                        var meter = (IAudioMeterInformation)control;
                        var channelBuffer = meter.GetMeteringChannelCount(out var channelCount) >= 0 &&
                                            channelCount > 0 && channelCount <= 32
                            ? new float[channelCount]
                            : [];
                        _meters.Add(meter);
                        _channelBuffers.Add(channelBuffer);
                        _meterProcessIds.Add(processId);
                        _meterControls.Add(control);
                        control = null;
                    }
                    catch (Exception) when (control is not null)
                    {
                        control = null;
                    }
                }
            }
            finally
            {
                sessions = null!;
            }

            if (logResult)
            {
                LogBindingResult("ready", _meters.Count, null);
            }
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException)
        {
            CleanupBinding();
            if (logResult)
            {
                LogBindingResult("failed", 0, exception);
            }
        }
    }

    private void LogBindingCheckpoint()
    {
        if (_log is null)
        {
            return;
        }

        _log.Write(
            TechnicalLogLevel.Information,
            TechnicalEventIds.MediaAudioMeterBinding,
            "MediaAudio",
            "Media audio meter binding is starting.",
            properties: new Dictionary<string, object?>
            {
                ["phase"] = "before-core-audio-enumeration",
                ["selected"] = _media.Current.HasMedia
            });
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            _log.FlushAsync(timeout.Token).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException)
        {
            // A diagnostic checkpoint must not prevent audio recovery.
        }
    }

    private void LogBindingResult(string state, int matchedCount, Exception? exception)
    {
        _log?.Write(
            exception is null ? TechnicalLogLevel.Information : TechnicalLogLevel.Warning,
            exception is null
                ? TechnicalEventIds.MediaAudioMeterBinding
                : TechnicalEventIds.MediaAudioMeterFailed,
            "MediaAudio",
            "Media audio meter binding completed.",
            exception,
            new Dictionary<string, object?>
            {
                ["state"] = state,
                ["matchedCount"] = matchedCount,
                ["hresult"] = exception?.HResult
            });
    }

    private static T Activate<T>(IMMDevice device, Guid interfaceId) where T : class
    {
        CoreAudioNative.ThrowIfFailed(device.Activate(ref interfaceId, ClsContext.All, 0, out var activated));
        return (T)activated;
    }

    private void OnMediaSnapshotChanged(object? sender, MediaSnapshot snapshot)
    {
        var next = MediaAudioBindingIdentity.From(snapshot);
        lock (_gate)
        {
            if (_disposed || next == _bindingIdentity)
            {
                return;
            }

            _bindingIdentity = next;
            _needsRebind = true;
            Interlocked.Exchange(ref _diagnosticCheckpointPending, 1);
            _wake.Set();
        }
    }

    private void OnMediaStateChanged(object? sender, MediaServiceState state) => RequestRebind();

    private void RequestRebind()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _needsRebind = true;
            Interlocked.Exchange(ref _diagnosticCheckpointPending, 1);
            _wake.Set();
        }
    }

    private bool TryReadPeaks(
        out float leftPeak,
        out float rightPeak,
        out uint activeProcessId)
    {
        leftPeak = 0;
        rightPeak = 0;
        activeProcessId = 0;
        var activeProcessPeak = -1f;
        var succeeded = false;
        for (var meterIndex = 0; meterIndex < _meters.Count; meterIndex++)
        {
            var meter = _meters[meterIndex];
            var channelPeaks = _channelBuffers[meterIndex];
            if (TryReadChannelPeaks(meter, channelPeaks, out var sessionLeft, out var sessionRight))
            {
                leftPeak = Math.Max(leftPeak, sessionLeft);
                rightPeak = Math.Max(rightPeak, sessionRight);
                var sessionPeak = Math.Max(sessionLeft, sessionRight);
                if (sessionPeak > activeProcessPeak)
                {
                    activeProcessPeak = sessionPeak;
                    activeProcessId = _meterProcessIds[meterIndex];
                }
                succeeded = true;
                continue;
            }

            try
            {
                if (meter.GetPeakValue(out var peak) >= 0)
                {
                    leftPeak = Math.Max(leftPeak, peak);
                    rightPeak = Math.Max(rightPeak, peak);
                    if (peak > activeProcessPeak)
                    {
                        activeProcessPeak = peak;
                        activeProcessId = _meterProcessIds[meterIndex];
                    }
                    succeeded = true;
                }
            }
            catch (Exception exception) when (
                exception is COMException or InvalidComObjectException)
            {
                _needsRebind = true;
            }
        }

        if (succeeded &&
            Math.Max(leftPeak, rightPeak) <= 0.001f &&
            _endpointMeter is not null)
        {
            if (TryReadChannelPeaks(
                    _endpointMeter,
                    _endpointChannelBuffer,
                    out var endpointLeft,
                    out var endpointRight))
            {
                leftPeak = endpointLeft;
                rightPeak = endpointRight;
            }
            else
            {
                try
                {
                    if (_endpointMeter.GetPeakValue(out var endpointPeak) >= 0)
                    {
                        leftPeak = rightPeak = endpointPeak;
                    }
                }
                catch (Exception exception) when (
                    exception is COMException or InvalidComObjectException)
                {
                    _needsRebind = true;
                }
            }
        }

        return succeeded;
    }

    private void EnsureSpectrumCapture(uint processId)
    {
        if (processId == 0)
        {
            return;
        }

        if (_loopbackCapture is not null && _loopbackProcessId != processId)
        {
            StopSpectrumCapture();
        }

        lock (_spectrumGate)
        {
            if (_loopbackCapture is not null || _meters.Count == 0 || _disposed)
            {
                return;
            }

            WasapiRecorder? capture = null;
            try
            {
                capture = new WasapiRecorderBuilder()
                    .WithProcessLoopback(
                        processId,
                        NAudio.CoreAudioApi.ProcessLoopbackMode.IncludeTargetProcessTree)
                    .WithBufferLength(40)
                    .BuildAsync()
                    .GetAwaiter()
                    .GetResult();
                _loopbackDataHandler = (buffer, _, _, _) => OnLoopbackDataAvailable(buffer);
                capture.DataAvailable += _loopbackDataHandler;
                capture.RecordingStopped += OnLoopbackRecordingStopped;
                _spectrumAnalyzer.Reset();
                _latestSpectrum = MediaAudioLevelSnapshot.Silent;
                _latestSpectrumTimestamp = 0;
                _spectrumDataObserved = false;
                _spectrumVisualActivityObserved = false;
                _spectrumProbeBufferCount = 0;
                _maximumObservedSample = 0;
                _sessionPeakFallbackObserved = false;
                _loopbackCapture = capture;
                _loopbackProcessId = processId;
                capture.StartRecording();
                _log?.Write(
                    TechnicalLogLevel.Information,
                    TechnicalEventIds.MediaAudioMeterBinding,
                    "MediaAudio",
                    $"Media spectrum loopback started for process {processId} " +
                    $"({ResolveProcessName(processId)}) using {capture.WaveFormat}.");
            }
            catch (Exception exception) when (
                exception is COMException or InvalidOperationException or NotSupportedException)
            {
                if (capture is not null)
                {
                    if (_loopbackDataHandler is not null)
                    {
                        capture.DataAvailable -= _loopbackDataHandler;
                    }

                    capture.RecordingStopped -= OnLoopbackRecordingStopped;
                    capture.Dispose();
                }

                _loopbackDataHandler = null;
                _loopbackCapture = null;
                _loopbackProcessId = 0;
                _latestSpectrum = MediaAudioLevelSnapshot.Silent;
                _latestSpectrumTimestamp = 0;
                _log?.Write(
                    TechnicalLogLevel.Warning,
                    TechnicalEventIds.MediaAudioMeterFailed,
                    "MediaAudio",
                    "Media spectrum loopback could not be started.",
                    exception);
            }
        }
    }

    private void OnLoopbackDataAvailable(ReadOnlySpan<byte> buffer)
    {
        lock (_spectrumGate)
        {
            if (_disposed ||
                _loopbackCapture is not { } capture ||
                capture.WaveFormat.BitsPerSample != 32 ||
                buffer.Length < sizeof(float))
            {
                return;
            }

            var byteCount = buffer.Length - buffer.Length % sizeof(float);
            var samples = MemoryMarshal.Cast<byte, float>(buffer[..byteCount]);
            for (var index = 0; index < samples.Length; index++)
            {
                if (float.IsFinite(samples[index]))
                {
                    _maximumObservedSample = Math.Max(
                        _maximumObservedSample,
                        Math.Abs(samples[index]));
                }
            }

            _spectrumProbeBufferCount++;
            if (_spectrumAnalyzer.ProcessFloat32(
                samples,
                capture.WaveFormat.SampleRate,
                capture.WaveFormat.Channels,
                out var spectrum))
            {
                _latestSpectrum = spectrum;
                _latestSpectrumTimestamp = Environment.TickCount64;
                if (!_spectrumDataObserved)
                {
                    _spectrumDataObserved = true;
                    _log?.Write(
                        TechnicalLogLevel.Information,
                        TechnicalEventIds.MediaAudioMeterBinding,
                        "MediaAudio",
                        "Media spectrum data is available.",
                        properties: new Dictionary<string, object?>
                        {
                            ["sampleRate"] = capture.WaveFormat.SampleRate,
                            ["channels"] = capture.WaveFormat.Channels
                        });
                }

                if (!_spectrumVisualActivityObserved &&
                    Math.Max(spectrum.Treble, Math.Max(spectrum.Bass, spectrum.Vocal)) >
                    AudioSpectrumAnalyzer.MinimumScale + 0.02)
                {
                    _spectrumVisualActivityObserved = true;
                    _log?.Write(
                        TechnicalLogLevel.Information,
                        TechnicalEventIds.MediaAudioMeterBinding,
                        "MediaAudio",
                        "Media spectrum contains visual activity.");
                }
            }

            if (_spectrumProbeBufferCount == 30 && !_spectrumVisualActivityObserved)
            {
                _log?.Write(
                    TechnicalLogLevel.Warning,
                    TechnicalEventIds.MediaAudioMeterFailed,
                    "MediaAudio",
                    $"Media spectrum loopback remained silent for process {_loopbackProcessId}; " +
                    $"maximum sample {_maximumObservedSample:F6}.");
            }
        }
    }

    private void OnLoopbackRecordingStopped(object? sender, StoppedEventArgs args)
    {
        if (_disposed)
        {
            return;
        }

        _log?.Write(
            TechnicalLogLevel.Warning,
            TechnicalEventIds.MediaAudioMeterFailed,
            "MediaAudio",
            "Media spectrum loopback stopped unexpectedly; rebinding.",
            args.Exception);
        RequestRebind();
    }

    private MediaAudioLevelSnapshot ReadSpectrumOrSessionPeak(float leftPeak, float rightPeak)
    {
        lock (_spectrumGate)
        {
            var spectrumHasActivity =
                Math.Max(
                    _latestSpectrum.Treble,
                    Math.Max(_latestSpectrum.Bass, _latestSpectrum.Vocal)) >
                AudioSpectrumAnalyzer.MinimumScale + 0.02;
            if (_loopbackCapture is not null &&
                _latestSpectrum.IsAvailable &&
                spectrumHasActivity &&
                Environment.TickCount64 - _latestSpectrumTimestamp <= 350)
            {
                return _latestSpectrum;
            }

            var fallback = _smoother.Update(leftPeak, rightPeak);
            if (!_sessionPeakFallbackObserved && Math.Max(leftPeak, rightPeak) > 0.001f)
            {
                _sessionPeakFallbackObserved = true;
                _log?.Write(
                    TechnicalLogLevel.Information,
                    TechnicalEventIds.MediaAudioMeterBinding,
                    "MediaAudio",
                    "Media visualizer is using the real Core Audio session-peak fallback.");
            }

            return fallback;
        }
    }

    private static bool TryReadChannelPeaks(
        IAudioMeterInformation meter,
        float[] channelPeaks,
        out float leftPeak,
        out float rightPeak)
    {
        leftPeak = 0;
        rightPeak = 0;
        if (channelPeaks.Length == 0)
        {
            return false;
        }

        try
        {
            if (meter.GetChannelsPeakValues((uint)channelPeaks.Length, channelPeaks) < 0)
            {
                return false;
            }

            leftPeak = channelPeaks[0];
            rightPeak = channelPeaks.Length > 1 ? channelPeaks[1] : leftPeak;
            for (var index = 2; index < channelPeaks.Length; index++)
            {
                if ((index & 1) == 0)
                {
                    leftPeak = Math.Max(leftPeak, channelPeaks[index]);
                }
                else
                {
                    rightPeak = Math.Max(rightPeak, channelPeaks[index]);
                }
            }

            return true;
        }
        catch (Exception exception) when (
            exception is COMException or InvalidComObjectException)
        {
            // Some audio drivers expose session peak data but not a reliable
            // per-channel buffer. The caller will use GetPeakValue instead.
            return false;
        }
    }

    private void Publish(MediaAudioLevelSnapshot snapshot)
    {
        if (_disposed || Current == snapshot)
        {
            return;
        }

        Current = snapshot;
        foreach (EventHandler<MediaAudioLevelSnapshot> handler in LevelChanged?.GetInvocationList() ?? [])
        {
            try
            {
                handler(this, snapshot);
            }
            catch
            {
                // Meter consumers can be removed while Core Audio is publishing.
            }
        }
    }

    private void CleanupBinding()
    {
        StopSpectrumCapture();
        _meters.Clear();
        _channelBuffers.Clear();
        _meterProcessIds.Clear();
        _meterControls.Clear();
        _sessionManager = null;
        _endpointMeter = null;
        _endpointChannelBuffer = [];
        _renderDevice = null;
    }

    private void StopSpectrumCapture()
    {
        WasapiRecorder? capture;
        lock (_spectrumGate)
        {
            capture = _loopbackCapture;
            _loopbackCapture = null;
            _loopbackProcessId = 0;
            _latestSpectrum = _spectrumAnalyzer.Reset();
            _latestSpectrumTimestamp = 0;
            _spectrumDataObserved = false;
        }

        if (capture is null)
        {
            return;
        }

        if (_loopbackDataHandler is not null)
        {
            capture.DataAvailable -= _loopbackDataHandler;
            _loopbackDataHandler = null;
        }
        capture.RecordingStopped -= OnLoopbackRecordingStopped;
        try
        {
            capture.StopRecording();
        }
        catch (Exception exception) when (
            exception is COMException or InvalidOperationException or ObjectDisposedException)
        {
            // The endpoint may disappear while the capture is being stopped.
        }
        finally
        {
            capture.Dispose();
        }
    }

    private void CleanupAudio()
    {
        CleanupBinding();
        if (_deviceEnumerator is not null && _deviceNotification is not null)
        {
            try
            {
                _deviceEnumerator.UnregisterEndpointNotificationCallback(_deviceNotification);
            }
            catch (Exception exception) when (
                exception is COMException or InvalidComObjectException)
            {
                // The endpoint can disappear while the worker is shutting down.
            }
        }

        _deviceEnumerator = null;
        _deviceNotification = null;
    }

    private void TryCleanupAudio()
    {
        try
        {
            CleanupAudio();
        }
        catch (Exception exception) when (
            exception is COMException or InvalidComObjectException)
        {
            _deviceEnumerator = null;
            _deviceNotification = null;
            _sessionManager = null;
            _renderDevice = null;
            _meters.Clear();
            _meterControls.Clear();
            _channelBuffers.Clear();
            _meterProcessIds.Clear();
            _endpointMeter = null;
            _endpointChannelBuffer = [];
        }
    }

    private static string ResolveProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            return process.ProcessName;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string ResolveProcessPath(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById(checked((int)processId));
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
                System.ComponentModel.Win32Exception or NotSupportedException)
        {
            return string.Empty;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        _media.SnapshotChanged -= OnMediaSnapshotChanged;
        _media.StateChanged -= OnMediaStateChanged;
        _wake.Set();
        if (_worker?.IsAlive == true)
        {
            _worker.Join(TimeSpan.FromSeconds(3));
        }

        if (_worker?.IsAlive != true)
        {
            _wake.Dispose();
        }
    }

    [ComVisible(true)]
    private sealed class DeviceNotificationCallback(Func<bool> isActive, Action changed)
        : IMMNotificationClient
    {
        public int OnDeviceStateChanged(string deviceId, uint newState)
        {
            RunSafely();

            return 0;
        }

        public int OnDeviceAdded(string deviceId)
        {
            RunSafely();

            return 0;
        }

        public int OnDeviceRemoved(string deviceId)
        {
            RunSafely();

            return 0;
        }

        public int OnDefaultDeviceChanged(AudioDataFlow flow, AudioDeviceRole role, string? defaultDeviceId)
        {
            RunSafely();

            return 0;
        }

        public int OnPropertyValueChanged(string deviceId, PropertyKey key) => 0;

        private void RunSafely()
        {
            try
            {
                if (isActive()) changed();
            }
            catch
            {
                // Never let a Core Audio reverse P/Invoke callback fail-fast MiaDock.
            }
        }
    }
}
