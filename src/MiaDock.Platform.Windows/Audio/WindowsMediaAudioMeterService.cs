using System.Diagnostics;
using System.Runtime.InteropServices;
using MiaDock.Core.Logging;
using MiaDock.Modules.Media.Models;
using MiaDock.Modules.Media.Services;

namespace MiaDock.Platform.Windows.Audio;

public sealed class WindowsMediaAudioMeterService : IMediaAudioMeterService
{
    private const int SampleIntervalMilliseconds = 33;
    private const int MeterRebindIntervalMilliseconds = 2000;
    private const int MeterReadGraceMilliseconds = 1500;
    private readonly IMediaSessionService _media;
    private readonly IMediaAudioVisualizerSettings _visualizerSettings;
    private readonly ILogService? _log;
    private readonly object _gate = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly AudioLevelSmoother _smoother = new();
    private readonly SpectrumPlaybackGate _spectrumPlaybackGate = new();
    private readonly WindowsSystemSpectrumSource _spectrum;
    private readonly List<IAudioSessionControl> _meterControls = [];
    private readonly List<IAudioMeterInformation> _meters = [];
    private readonly List<float[]> _channelBuffers = [];
    private Thread? _worker;
    private bool _active;
    private volatile bool _needsMeterRebind = true;
    private volatile bool _experimentalSpectrumEnabled;
    private string? _spectrumOutputDeviceId;
    private int _spectrumEndpointSelectionPending;
    private int _spectrumEndpointChangePending;
    private int _diagnosticCheckpointPending = 1;
    private bool _disposed;
    private IMMDeviceEnumerator? _deviceEnumerator;
    private IMMNotificationClient? _deviceNotification;
    private IMMDevice? _renderDevice;
    private string? _renderEndpointId;
    private string? _renderEndpointName;
    private IAudioSessionManager2? _sessionManager;
    private MediaAudioBindingIdentity _bindingIdentity;
    private long _nextMeterRebindAt;
    private long _lastSuccessfulMeterRead;
    private float _lastLeftPeak;
    private float _lastRightPeak;
    private bool _lastSessionPeakAvailable;
    private MediaAudioLevelSource _lastPublishedSource = MediaAudioLevelSource.Unavailable;

    public WindowsMediaAudioMeterService(
        IMediaSessionService media,
        IMediaAudioVisualizerSettings visualizerSettings,
        ILogService? log = null)
        : this(media, visualizerSettings, new WindowsSystemLoopbackCaptureFactory(), log)
    {
    }

    internal WindowsMediaAudioMeterService(
        IMediaSessionService media,
        IMediaAudioVisualizerSettings visualizerSettings,
        ISystemLoopbackCaptureFactory captureFactory,
        ILogService? log = null)
    {
        _media = media ?? throw new ArgumentNullException(nameof(media));
        _visualizerSettings = visualizerSettings ??
            throw new ArgumentNullException(nameof(visualizerSettings));
        _log = log;
        _experimentalSpectrumEnabled = visualizerSettings.ExperimentalSpectrumEnabled;
        _spectrumOutputDeviceId = visualizerSettings.SpectrumOutputDeviceId;
        _bindingIdentity = MediaAudioBindingIdentity.From(media.Current);
        _spectrum = new WindowsSystemSpectrumSource(captureFactory, log, WakeWorker);
        _spectrum.SelectEndpoint(_spectrumOutputDeviceId);
        _media.SnapshotChanged += OnMediaSnapshotChanged;
        _media.StateChanged += OnMediaStateChanged;
        _visualizerSettings.ExperimentalSpectrumEnabledChanged += OnExperimentalSpectrumEnabledChanged;
        _visualizerSettings.SpectrumOutputDeviceIdChanged += OnSpectrumOutputDeviceIdChanged;
        LogExperimentalSpectrumMode(_experimentalSpectrumEnabled);
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

                var media = _media.Current;
                var now = Environment.TickCount64;
                var spectrumIsFresh = _spectrum.TryGetSnapshot(now, out var spectrum);
                var outputSignalActive = spectrumIsFresh &&
                    Math.Max(spectrum.Treble, Math.Max(spectrum.Bass, spectrum.Vocal)) >
                    AudioSpectrumAnalyzer.MinimumScale + 0.025;
                var shouldRunSpectrum = _spectrumPlaybackGate.Evaluate(
                    _experimentalSpectrumEnabled,
                    active,
                    media.HasMedia,
                    media.PlaybackStatus,
                    outputSignalActive,
                    now);
                if (!active || !media.HasMedia && !shouldRunSpectrum)
                {
                    _spectrum.Stop();
                    Publish(_smoother.Reset());
                    _wake.WaitOne();
                    continue;
                }

                if (Interlocked.Exchange(ref _spectrumEndpointSelectionPending, 0) != 0)
                {
                    _spectrum.SelectEndpoint(_spectrumOutputDeviceId);
                }

                if (Interlocked.Exchange(ref _spectrumEndpointChangePending, 0) != 0)
                {
                    _spectrum.RestartForEndpointChange();
                }

                if (media.HasMedia && (_needsMeterRebind || now >= _nextMeterRebindAt))
                {
                    var diagnosticCheckpoint =
                        Interlocked.Exchange(ref _diagnosticCheckpointPending, 0) != 0;
                    if (diagnosticCheckpoint)
                    {
                        LogBindingCheckpoint();
                    }

                    BindMeter(diagnosticCheckpoint);
                    _nextMeterRebindAt = now + MeterRebindIntervalMilliseconds;
                }

                if (shouldRunSpectrum)
                {
                    _spectrum.EnsureRunning(now);
                }
                else
                {
                    _spectrum.Stop();
                }

                var sessionPeak = media.HasMedia
                    ? ReadSessionPeak(now)
                    : _smoother.Reset();
                var selected = MediaAudioCaptureDecision.Select(
                    shouldRunSpectrum,
                    spectrum,
                    spectrumIsFresh,
                    sessionPeak);
                LogSourceTransition(selected.Source);
                Publish(selected);
                _wake.WaitOne(TimeSpan.FromMilliseconds(
                    shouldRunSpectrum || _meters.Count > 0
                        ? SampleIntervalMilliseconds
                        : MeterRebindIntervalMilliseconds));
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

    private MediaAudioLevelSnapshot ReadSessionPeak(long now)
    {
        if (TryReadPeaks(out var leftPeak, out var rightPeak))
        {
            _lastLeftPeak = leftPeak;
            _lastRightPeak = rightPeak;
            _lastSessionPeakAvailable = true;
            _lastSuccessfulMeterRead = now;
            return _smoother.Update(leftPeak, rightPeak);
        }

        if (_lastSessionPeakAvailable && now - _lastSuccessfulMeterRead <= MeterReadGraceMilliseconds)
        {
            return _smoother.Update(_lastLeftPeak, _lastRightPeak);
        }

        _lastSessionPeakAvailable = false;
        return _smoother.Reset();
    }

    private void InitializeDeviceNotifications()
    {
        _deviceEnumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
        _deviceNotification = new DeviceNotificationCallback(
            () => !_disposed,
            IsCurrentRenderEndpoint,
            RequestAudioTopologyRebind);
        CoreAudioNative.ThrowIfFailed(
            _deviceEnumerator.RegisterEndpointNotificationCallback(_deviceNotification));
    }

    private void BindMeter(bool logResult)
    {
        CleanupBinding();
        _needsMeterRebind = false;
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
                AudioDataFlow.Render,
                AudioDeviceRole.Multimedia,
                out _renderDevice!));
            CoreAudioNative.ThrowIfFailed(_renderDevice.GetId(out _renderEndpointId!));
            _renderEndpointName = ReadDeviceFriendlyName(_renderDevice);
            _sessionManager = Activate<IAudioSessionManager2>(
                _renderDevice,
                CoreAudioNative.SessionManagerId);
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
                                            channelCount is > 0 and <= 32
                            ? new float[channelCount]
                            : [];
                        _meters.Add(meter);
                        _channelBuffers.Add(channelBuffer);
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
        catch (Exception exception) when (
            exception is COMException or InvalidComObjectException or InvalidCastException)
        {
            CleanupBinding();
            if (logResult)
            {
                LogBindingResult("failed", 0, exception);
            }
        }
    }

    private bool TryReadPeaks(out float leftPeak, out float rightPeak)
    {
        leftPeak = 0;
        rightPeak = 0;
        var succeeded = false;
        for (var meterIndex = 0; meterIndex < _meters.Count; meterIndex++)
        {
            var meter = _meters[meterIndex];
            var channelPeaks = _channelBuffers[meterIndex];
            if (TryReadChannelPeaks(meter, channelPeaks, out var sessionLeft, out var sessionRight))
            {
                leftPeak = Math.Max(leftPeak, sessionLeft);
                rightPeak = Math.Max(rightPeak, sessionRight);
                succeeded = true;
                continue;
            }

            try
            {
                if (meter.GetPeakValue(out var peak) >= 0)
                {
                    leftPeak = Math.Max(leftPeak, peak);
                    rightPeak = Math.Max(rightPeak, peak);
                    succeeded = true;
                }
            }
            catch (Exception exception) when (
                exception is COMException or InvalidComObjectException)
            {
                _needsMeterRebind = true;
            }
        }

        return succeeded;
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
            return false;
        }
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
            _needsMeterRebind = true;
            _nextMeterRebindAt = 0;
            Interlocked.Exchange(ref _diagnosticCheckpointPending, 1);
            _wake.Set();
        }
    }

    private void OnMediaStateChanged(object? sender, MediaServiceState state) => RequestMeterRebind();

    private void OnExperimentalSpectrumEnabledChanged(object? sender, bool enabled)
    {
        lock (_gate)
        {
            if (_disposed || enabled == _experimentalSpectrumEnabled)
            {
                return;
            }

            _experimentalSpectrumEnabled = enabled;
            _wake.Set();
        }

        LogExperimentalSpectrumMode(enabled);
    }

    private void OnSpectrumOutputDeviceIdChanged(object? sender, string? endpointId)
    {
        endpointId = string.IsNullOrWhiteSpace(endpointId) ? null : endpointId.Trim();
        lock (_gate)
        {
            if (_disposed || string.Equals(
                    endpointId,
                    _spectrumOutputDeviceId,
                    StringComparison.Ordinal))
            {
                return;
            }

            _spectrumOutputDeviceId = endpointId;
            Interlocked.Exchange(ref _spectrumEndpointSelectionPending, 1);
            _wake.Set();
        }

        _log?.Write(
            TechnicalLogLevel.Information,
            TechnicalEventIds.MediaAudioMeterBinding,
            "MediaAudio",
            "Experimental system spectrum output device changed.",
            properties: new Dictionary<string, object?>
            {
                ["requestedEndpointId"] = endpointId,
                ["usesDefaultEndpoint"] = endpointId is null
            });
    }

    private void RequestMeterRebind()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _needsMeterRebind = true;
            _nextMeterRebindAt = 0;
            Interlocked.Exchange(ref _diagnosticCheckpointPending, 1);
            _wake.Set();
        }
    }

    private void RequestAudioTopologyRebind(bool currentSpectrumEndpointChanged)
    {
        if (currentSpectrumEndpointChanged || string.IsNullOrWhiteSpace(_spectrumOutputDeviceId))
        {
            Interlocked.Exchange(ref _spectrumEndpointChangePending, 1);
        }

        RequestMeterRebind();
    }

    private bool IsCurrentRenderEndpoint(string? deviceId) =>
        !string.IsNullOrWhiteSpace(deviceId) &&
        (string.Equals(deviceId, _renderEndpointId, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(deviceId, _spectrum.EndpointId, StringComparison.OrdinalIgnoreCase));

    private void WakeWorker()
    {
        try
        {
            _wake.Set();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void LogExperimentalSpectrumMode(bool enabled) =>
        _log?.Write(
            TechnicalLogLevel.Information,
            TechnicalEventIds.MediaAudioMeterBinding,
            "MediaAudio",
            enabled
                ? "Experimental system spectrum enabled."
                : "Experimental system spectrum disabled; SessionPeak is active.",
            properties: new Dictionary<string, object?>
            {
                ["experimentalSpectrumEnabled"] = enabled,
                ["defaultSourceMode"] = MediaAudioLevelSource.SessionPeak.ToString()
            });

    private void LogSourceTransition(MediaAudioLevelSource source)
    {
        if (_lastPublishedSource == source)
        {
            return;
        }

        var previous = _lastPublishedSource;
        _lastPublishedSource = source;
        _log?.Write(
            TechnicalLogLevel.Information,
            TechnicalEventIds.MediaAudioMeterBinding,
            "MediaAudio",
            $"Media visualizer source changed from {previous} to {source}.",
            properties: new Dictionary<string, object?>
            {
                ["previousSourceMode"] = previous.ToString(),
                ["sourceMode"] = source.ToString(),
                ["experimentalSpectrumEnabled"] = _experimentalSpectrumEnabled
            });
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
                ["sourceApp"] = _media.Current.Source.Id,
                ["renderEndpointId"] = _renderEndpointId,
                ["renderEndpointName"] = _renderEndpointName,
                ["matchedCount"] = matchedCount,
                ["hresult"] = exception?.HResult
            });
    }

    private void Publish(MediaAudioLevelSnapshot snapshot)
    {
        if (_disposed || Current == snapshot)
        {
            return;
        }

        Current = snapshot;
        foreach (EventHandler<MediaAudioLevelSnapshot> handler in
                 LevelChanged?.GetInvocationList() ?? [])
        {
            try
            {
                handler(this, snapshot);
            }
            catch
            {
            }
        }
    }

    private void CleanupBinding()
    {
        _lastLeftPeak = _lastRightPeak = 0;
        _lastSessionPeakAvailable = false;
        _lastSuccessfulMeterRead = 0;
        _meters.Clear();
        _channelBuffers.Clear();
        _meterControls.Clear();
        _sessionManager = null;
        _renderDevice = null;
        _renderEndpointId = null;
        _renderEndpointName = null;
    }

    private void CleanupAudio()
    {
        _spectrum.Dispose();
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
            exception is COMException or InvalidComObjectException or InvalidOperationException)
        {
            _deviceEnumerator = null;
            _deviceNotification = null;
            _sessionManager = null;
            _renderDevice = null;
            _meters.Clear();
            _meterControls.Clear();
            _channelBuffers.Clear();
        }
    }

    private static T Activate<T>(IMMDevice device, Guid interfaceId) where T : class
    {
        CoreAudioNative.ThrowIfFailed(
            device.Activate(ref interfaceId, ClsContext.All, 0, out var activated));
        return (T)activated;
    }

    private static string? ReadDeviceFriendlyName(IMMDevice device)
    {
        IPropertyStore? properties = null;
        try
        {
            if (device.OpenPropertyStore(CoreAudioNative.StorageModeRead, out properties) < 0)
            {
                return null;
            }

            var key = CoreAudioNative.DeviceFriendlyNameKey;
            if (properties.GetValue(ref key, out var value) < 0)
            {
                return null;
            }

            try
            {
                return value.ValueType == CoreAudioNative.VariantTypeStringPointer &&
                       value.PointerValue != 0
                    ? Marshal.PtrToStringUni(value.PointerValue)?.Trim()
                    : null;
            }
            finally
            {
                CoreAudioNative.PropVariantClear(ref value);
            }
        }
        catch (Exception exception) when (
            exception is COMException or InvalidComObjectException or InvalidCastException)
        {
            return null;
        }
        finally
        {
            properties = null;
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
        _visualizerSettings.ExperimentalSpectrumEnabledChanged -=
            OnExperimentalSpectrumEnabledChanged;
        _visualizerSettings.SpectrumOutputDeviceIdChanged -= OnSpectrumOutputDeviceIdChanged;
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
    private sealed class DeviceNotificationCallback(
        Func<bool> isActive,
        Func<string?, bool> isCurrentEndpoint,
        Action<bool> changed) : IMMNotificationClient
    {
        public int OnDeviceStateChanged(string deviceId, uint newState)
        {
            if (isCurrentEndpoint(deviceId))
            {
                RunSafely(currentSpectrumEndpointChanged: true);
            }

            return 0;
        }

        public int OnDeviceAdded(string deviceId) => 0;

        public int OnDeviceRemoved(string deviceId)
        {
            if (isCurrentEndpoint(deviceId))
            {
                RunSafely(currentSpectrumEndpointChanged: true);
            }

            return 0;
        }

        public int OnDefaultDeviceChanged(
            AudioDataFlow flow,
            AudioDeviceRole role,
            string? defaultDeviceId)
        {
            if (flow == AudioDataFlow.Render && role == AudioDeviceRole.Multimedia)
            {
                RunSafely(currentSpectrumEndpointChanged: false);
            }

            return 0;
        }

        public int OnPropertyValueChanged(string deviceId, PropertyKey key) => 0;

        private void RunSafely(bool currentSpectrumEndpointChanged)
        {
            try
            {
                if (isActive())
                {
                    changed(currentSpectrumEndpointChanged);
                }
            }
            catch
            {
            }
        }
    }
}
