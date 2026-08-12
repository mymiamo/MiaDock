using System.Diagnostics;
using System.Runtime.InteropServices;
using MiaDock.Modules.Media.Models;
using MiaDock.Modules.Media.Services;
using MiaDock.Core.Logging;

namespace MiaDock.Platform.Windows.Audio;

public sealed class WindowsMediaAudioMeterService : IMediaAudioMeterService
{
    private readonly IMediaSessionService _media;
    private readonly ILogService? _log;
    private readonly object _gate = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly AudioLevelSmoother _smoother = new();
    private Thread? _worker;
    private bool _active;
    private volatile bool _needsRebind = true;
    private int _diagnosticCheckpointPending = 1;
    private bool _disposed;
    private IMMDeviceEnumerator? _deviceEnumerator;
    private IMMNotificationClient? _deviceNotification;
    private IMMDevice? _renderDevice;
    private IAudioSessionManager2? _sessionManager;
    private readonly List<IAudioSessionControl> _meterControls = [];
    private readonly List<IAudioMeterInformation> _meters = [];
    private readonly List<float[]> _channelBuffers = [];
    private MediaAudioBindingIdentity _bindingIdentity;

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

                if (!active || !_media.Current.HasMedia)
                {
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

                if (TryReadPeaks(out var leftPeak, out var rightPeak))
                {
                    Publish(_smoother.Update(leftPeak, rightPeak));
                    _wake.WaitOne(TimeSpan.FromMilliseconds(66));
                }
                else
                {
                    _needsRebind = true;
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
                        if (!MediaAudioSessionMatcher.IsMatch(sourceId, processName))
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

    private bool TryReadPeaks(out float leftPeak, out float rightPeak)
    {
        leftPeak = 0;
        rightPeak = 0;
        var succeeded = false;
        for (var meterIndex = 0; meterIndex < _meters.Count; meterIndex++)
        {
            var meter = _meters[meterIndex];
            try
            {
                var channelPeaks = _channelBuffers[meterIndex];
                if (channelPeaks.Length > 0 &&
                    meter.GetChannelsPeakValues((uint)channelPeaks.Length, channelPeaks) >= 0)
                {
                    var sessionLeft = channelPeaks[0];
                    var sessionRight = channelPeaks.Length > 1 ? channelPeaks[1] : sessionLeft;
                    for (var index = 2; index < channelPeaks.Length; index++)
                    {
                        if ((index & 1) == 0)
                        {
                            sessionLeft = Math.Max(sessionLeft, channelPeaks[index]);
                        }
                        else
                        {
                            sessionRight = Math.Max(sessionRight, channelPeaks[index]);
                        }
                    }

                    leftPeak = Math.Max(leftPeak, sessionLeft);
                    rightPeak = Math.Max(rightPeak, sessionRight);
                    succeeded = true;
                    continue;
                }

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
                _needsRebind = true;
            }
        }

        return succeeded;
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
        _meters.Clear();
        _channelBuffers.Clear();
        _meterControls.Clear();
        _sessionManager = null;
        _renderDevice = null;
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
