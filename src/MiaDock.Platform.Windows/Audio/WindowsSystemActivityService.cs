using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using MiaDock.Core.Threading;
using MiaDock.Core.Logging;
using MiaDock.Modules.Media.Models;
using MiaDock.Modules.Media.Services;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.Services;
using MiaDock.Platform.Windows.Threading;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Security.Authorization.AppCapabilityAccess;

namespace MiaDock.Platform.Windows.Audio;

public sealed class WindowsSystemActivityService : ISystemActivityService
{
    private static readonly Guid EventContext = new("1C7FD31E-7D88-45CA-BA36-56DFEA691D08");
    private const int MaximumQueuedWorkItems = 256;

    private readonly IUiDispatcher _dispatcher;
    private readonly IMediaSessionService _media;
    private readonly ILogService _log;
    private readonly BlockingCollection<Action> _workItems =
        new(new ConcurrentQueue<Action>(), MaximumQueuedWorkItems);
    private readonly CoalescingActionScheduler _refreshScheduler;
    private readonly CoalescingActionScheduler _rebindScheduler;
    private readonly object _stateGate = new();
    private readonly object _startGate = new();
    private readonly HashSet<string> _cameraDeviceIds = new(StringComparer.Ordinal);
    private readonly List<AudioSessionHandle> _sessions = [];
    private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Thread? _worker;
    private IMMDeviceEnumerator? _deviceEnumerator;
    private EndpointNotificationCallback? _deviceNotification;
    private IMMDevice? _renderDevice;
    private IAudioEndpointVolume? _endpointVolume;
    private EndpointVolumeCallback? _endpointVolumeCallback;
    private IMMDevice? _captureDevice;
    private IAudioSessionManager2? _renderSessionManager;
    private IAudioSessionManager2? _captureSessionManager;
    private SessionNotificationCallback? _renderSessionNotification;
    private SessionNotificationCallback? _captureSessionNotification;
    private AudioSessionHandle? _applicationSession;
    private DeviceWatcher? _cameraWatcher;
    private AppCapability? _cameraCapability;
    private string? _selectedMediaSourceId;
    private bool _audioAvailable;
    private bool _captureAvailable;
    private bool _cameraApiAvailable;
    private bool _disposed;
    private bool _initialStateLogged;
    private bool _initializing = true;
    private SystemActivitySnapshot _pendingSnapshot = SystemActivitySnapshot.Default;
    private long _pendingSnapshotVersion;
    private int _snapshotDispatchPending;

    public WindowsSystemActivityService(
        IUiDispatcher dispatcher,
        IMediaSessionService media,
        ILogService log)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _media = media ?? throw new ArgumentNullException(nameof(media));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        Interlocked.Exchange(ref _selectedMediaSourceId, ResolveSelectedMediaSourceId());
        _refreshScheduler = new CoalescingActionScheduler(Post, RefreshAndPublish);
        _rebindScheduler = new CoalescingActionScheduler(Post, RebindAudio);
        _media.SnapshotChanged += OnMediaSnapshotChanged;
        _media.StateChanged += OnMediaStateChanged;
    }

    public SystemActivitySnapshot Current { get; private set; } = SystemActivitySnapshot.Default;

    public event EventHandler<SystemActivitySnapshot>? SnapshotChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_startGate)
        {
            if (_worker is null)
            {
                SetCurrent(Current with { ServiceState = SystemActivityServiceState.Initializing });
                _worker = new Thread(WorkerMain)
                {
                    IsBackground = true,
                    Name = "MiaDock.SystemActivity"
                };
                _worker.SetApartmentState(ApartmentState.MTA);
                _worker.Start();
            }
        }

        await _started.Task.WaitAsync(cancellationToken);
    }

    public Task<bool> SetMasterVolumeAsync(double volume, CancellationToken cancellationToken = default) =>
        ExecuteAsync(() =>
        {
            if (_endpointVolume is null)
            {
                return false;
            }

            var context = EventContext;
            CoreAudioNative.ThrowIfFailed(_endpointVolume.SetMasterVolumeLevelScalar(
                (float)Math.Clamp(volume, 0, 1), ref context));
            RefreshAndPublish();
            return true;
        }, cancellationToken);

    public Task<bool> ToggleMasterMuteAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(() =>
        {
            if (_endpointVolume is null || _endpointVolume.GetMute(out var muted) < 0)
            {
                return false;
            }

            var context = EventContext;
            CoreAudioNative.ThrowIfFailed(_endpointVolume.SetMute(!muted, ref context));
            RefreshAndPublish();
            return true;
        }, cancellationToken);

    public Task<bool> SetApplicationVolumeAsync(double volume, CancellationToken cancellationToken = default) =>
        ExecuteAsync(() =>
        {
            if (_applicationSession is null)
            {
                return false;
            }

            var context = EventContext;
            CoreAudioNative.ThrowIfFailed(_applicationSession.Volume.SetMasterVolume(
                (float)Math.Clamp(volume, 0, 1), ref context));
            RefreshAndPublish();
            return true;
        }, cancellationToken);

    public Task<bool> ToggleApplicationMuteAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(() =>
        {
            if (_applicationSession is null || _applicationSession.Volume.GetMute(out var muted) < 0)
            {
                return false;
            }

            var context = EventContext;
            CoreAudioNative.ThrowIfFailed(_applicationSession.Volume.SetMute(!muted, ref context));
            RefreshAndPublish();
            return true;
        }, cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _media.SnapshotChanged -= OnMediaSnapshotChanged;
        _media.StateChanged -= OnMediaStateChanged;
        _workItems.CompleteAdding();
        if (_worker is not null && _worker.IsAlive)
        {
            await Task.Run(() => _worker.Join(TimeSpan.FromSeconds(5)));
        }

        _workItems.Dispose();
    }

    private void WorkerMain()
    {
        var comInitialized = CoreAudioNative.CoInitializeEx(0, CoreAudioNative.CoinitMultithreaded) >= 0;
        try
        {
            InitializeAudio();
            InitializeCamera();
            _initializing = false;
            RefreshAndPublish();
            _started.TrySetResult();

            foreach (var workItem in _workItems.GetConsumingEnumerable())
            {
                try
                {
                    workItem();
                }
                catch (Exception)
                {
                    RefreshAndPublish(SystemActivityServiceState.PartiallyAvailable);
                }
            }
        }
        catch (Exception)
        {
            _initializing = false;
            RefreshAndPublish(SystemActivityServiceState.Faulted);
            _started.TrySetResult();
        }
        finally
        {
            CleanupCamera();
            CleanupAudio();
            if (comInitialized)
            {
                CoreAudioNative.CoUninitialize();
            }

            _started.TrySetResult();
        }
    }

    private void InitializeAudio()
    {
        try
        {
            _deviceEnumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
            _deviceNotification = new EndpointNotificationCallback(PostRebindAudio);
            CoreAudioNative.ThrowIfFailed(
                _deviceEnumerator.RegisterEndpointNotificationCallback(_deviceNotification));
            RebindAudio();
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException)
        {
            _audioAvailable = false;
            _captureAvailable = false;
        }
    }

    private void RebindAudio()
    {
        CleanupAudioBindings();
        if (_deviceEnumerator is null)
        {
            return;
        }

        try
        {
            CoreAudioNative.ThrowIfFailed(GetDefaultEndpoint(
                AudioDataFlow.Render,
                AudioDeviceRole.Multimedia,
                AudioDeviceRole.Console,
                out _renderDevice));
            _endpointVolume = Activate<IAudioEndpointVolume>(_renderDevice, CoreAudioNative.EndpointVolumeId);
            _endpointVolumeCallback = new EndpointVolumeCallback(RequestRefresh);
            CoreAudioNative.ThrowIfFailed(
                _endpointVolume.RegisterControlChangeNotify(_endpointVolumeCallback));
            _renderSessionManager = Activate<IAudioSessionManager2>(
                _renderDevice, CoreAudioNative.SessionManagerId);
            _renderSessionNotification = new SessionNotificationCallback(PostRebindAudio);
            CoreAudioNative.ThrowIfFailed(
                _renderSessionManager.RegisterSessionNotification(_renderSessionNotification));
            EnumerateSessions(_renderSessionManager, AudioDataFlow.Render);
            _audioAvailable = true;
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException)
        {
            _audioAvailable = false;
        }

        try
        {
            CoreAudioNative.ThrowIfFailed(GetDefaultEndpoint(
                AudioDataFlow.Capture,
                AudioDeviceRole.Communications,
                AudioDeviceRole.Console,
                out _captureDevice));
            _captureSessionManager = Activate<IAudioSessionManager2>(
                _captureDevice, CoreAudioNative.SessionManagerId);
            _captureSessionNotification = new SessionNotificationCallback(PostRebindAudio);
            CoreAudioNative.ThrowIfFailed(
                _captureSessionManager.RegisterSessionNotification(_captureSessionNotification));
            EnumerateSessions(_captureSessionManager, AudioDataFlow.Capture);
            _captureAvailable = true;
        }
        catch (Exception exception) when (exception is COMException or InvalidCastException)
        {
            _captureAvailable = false;
        }

        RefreshAndPublish();
    }

    private void EnumerateSessions(IAudioSessionManager2 manager, AudioDataFlow flow)
    {
        CoreAudioNative.ThrowIfFailed(manager.GetSessionEnumerator(out var enumerator));
        try
        {
            CoreAudioNative.ThrowIfFailed(enumerator.GetCount(out var count));
            for (var index = 0; index < count; index++)
            {
                IAudioSessionControl? control = null;
                try
                {
                    CoreAudioNative.ThrowIfFailed(enumerator.GetSession(index, out control));
                    _sessions.Add(new AudioSessionHandle(
                        control,
                        flow,
                        RequestRefresh));
                    control = null;
                }
                catch (Exception)
                {
                    ReleaseComObject(control);
                }
            }
        }
        finally
        {
            ReleaseComObject(enumerator);
        }
    }

    private void InitializeCamera()
    {
        try
        {
            _cameraCapability = AppCapability.Create("webcam");
            _cameraCapability.AccessChanged += OnCameraAccessChanged;
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException)
        {
            _cameraCapability = null;
        }

        try
        {
            _cameraWatcher = DeviceInformation.CreateWatcher(DeviceClass.VideoCapture);
            _cameraWatcher.Added += OnCameraAdded;
            _cameraWatcher.Removed += OnCameraRemoved;
            _cameraWatcher.EnumerationCompleted += OnCameraEnumerationCompleted;
            _cameraWatcher.Start();
            _cameraApiAvailable = true;
        }
        catch (Exception exception) when (exception is COMException or UnauthorizedAccessException)
        {
            _cameraApiAvailable = false;
            _log.Write(
                TechnicalLogLevel.Warning,
                TechnicalEventIds.CameraWatcherUnavailable,
                "SystemActivity",
                "Camera device presence watcher is unavailable.",
                exception,
                new Dictionary<string, object?> { ["hresult"] = exception.HResult });
        }
    }

    private void RefreshAndPublish() => RefreshAndPublish(null);

    private void RefreshAndPublish(SystemActivityServiceState? forcedState)
    {
        _selectedMediaSourceId = ResolveSelectedMediaSourceId();
        var masterVolume = 0f;
        var masterMuted = false;
        var masterAvailable = _endpointVolume is not null &&
                              _endpointVolume.GetMasterVolumeLevelScalar(out masterVolume) >= 0 &&
                              _endpointVolume.GetMute(out masterMuted) >= 0;

        foreach (var session in _sessions.ToArray())
        {
            try
            {
                session.Refresh();
            }
            catch (COMException)
            {
            }
        }

        var microphoneActive = _captureAvailable && _sessions.Any(session =>
            session.Flow == AudioDataFlow.Capture &&
            session.State == AudioSessionState.Active &&
            session.ProcessId != Environment.ProcessId);
        var activeProcessNames = _sessions
            .Where(session => session.State == AudioSessionState.Active &&
                              !string.IsNullOrWhiteSpace(session.ProcessName))
            .Select(session => session.ProcessName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _applicationSession = null;
        var appAvailability = ApplicationVolumeAvailability.Unavailable;
        if (_audioAvailable)
        {
            if (string.IsNullOrWhiteSpace(_selectedMediaSourceId))
            {
                appAvailability = ApplicationVolumeAvailability.NoSelectedApplication;
            }
            else
            {
                _applicationSession = _sessions
                    .Where(session => session.Flow == AudioDataFlow.Render &&
                                      MediaAudioSessionMatcher.IsMatch(
                                          _selectedMediaSourceId, session.ProcessName))
                    .OrderByDescending(session => session.State == AudioSessionState.Active)
                    .FirstOrDefault();
                appAvailability = _applicationSession is null
                    ? ApplicationVolumeAvailability.SessionNotFound
                    : ApplicationVolumeAvailability.Available;
            }
        }

        var cameraAccess = CameraAccessState.Unavailable;
        if (_cameraApiAvailable && _cameraCapability is not null)
        {
            try
            {
                cameraAccess = CameraAccessMapper.Map(_cameraCapability.CheckAccess());
            }
            catch (COMException)
            {
                cameraAccess = CameraAccessState.Unavailable;
            }
        }

        var state = forcedState ?? ResolveServiceState(masterAvailable);
        var snapshot = new SystemActivitySnapshot(
            state,
            masterAvailable,
            masterAvailable ? masterVolume : 0,
            masterAvailable && masterMuted,
            appAvailability,
            _applicationSession?.VolumeLevel ?? 0,
            _applicationSession?.IsMuted ?? false,
            !_captureAvailable
                ? MicrophoneUsageState.Unavailable
                : microphoneActive ? MicrophoneUsageState.Active : MicrophoneUsageState.Idle,
            !_cameraApiAvailable
                ? CameraDeviceAvailability.Unavailable
                : _cameraDeviceIds.Count > 0
                    ? CameraDeviceAvailability.Available
                    : CameraDeviceAvailability.NotFound,
            cameraAccess,
            CommunicationActivityClassifier.Classify(microphoneActive, activeProcessNames));
        SetCurrent(snapshot);
        if (!_initializing && !_initialStateLogged && state is not SystemActivityServiceState.Initializing)
        {
            _initialStateLogged = true;
            _log.Write(
                state == SystemActivityServiceState.Faulted
                    ? TechnicalLogLevel.Warning
                    : TechnicalLogLevel.Information,
                TechnicalEventIds.SystemActivityReady,
                "SystemActivity",
                "System activity services initialized.",
                properties: new Dictionary<string, object?>
                {
                    ["state"] = state.ToString(),
                    ["status"] = $"master={masterAvailable};capture={_captureAvailable};cameraApi={_cameraApiAvailable};cameraPresent={_cameraDeviceIds.Count > 0}"
                });
        }
    }

    private SystemActivityServiceState ResolveServiceState(bool masterAvailable)
    {
        var availableCount = (masterAvailable ? 1 : 0) + (_captureAvailable ? 1 : 0) +
                             (_cameraApiAvailable ? 1 : 0);
        return availableCount switch
        {
            3 => SystemActivityServiceState.Ready,
            > 0 => SystemActivityServiceState.PartiallyAvailable,
            _ => SystemActivityServiceState.Unavailable
        };
    }

    private void SetCurrent(SystemActivitySnapshot snapshot)
    {
        lock (_stateGate)
        {
            if (Current == snapshot)
            {
                return;
            }

            Current = snapshot;
            _pendingSnapshot = snapshot;
            _pendingSnapshotVersion++;
        }

        QueueSnapshotDispatch();
    }

    private void QueueSnapshotDispatch()
    {
        if (_disposed ||
            Interlocked.CompareExchange(ref _snapshotDispatchPending, 1, 0) != 0)
        {
            return;
        }

        if (_dispatcher.HasThreadAccess)
        {
            DrainLatestSnapshot();
            return;
        }

        if (!_dispatcher.TryEnqueue(DrainLatestSnapshot))
        {
            Volatile.Write(ref _snapshotDispatchPending, 0);
        }
    }

    private void DrainLatestSnapshot()
    {
        SystemActivitySnapshot snapshot;
        long version;
        lock (_stateGate)
        {
            snapshot = _pendingSnapshot;
            version = _pendingSnapshotVersion;
        }

        var shouldReschedule = false;
        try
        {
            if (!_disposed)
            {
                SnapshotChanged?.Invoke(this, snapshot);
            }
        }
        finally
        {
            Volatile.Write(ref _snapshotDispatchPending, 0);
            lock (_stateGate)
            {
                shouldReschedule = !_disposed && version != _pendingSnapshotVersion;
            }

            if (shouldReschedule)
            {
                QueueSnapshotDispatch();
            }
        }
    }

    private Task<bool> ExecuteAsync(Func<bool> action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (_disposed || _worker is null || !_worker.IsAlive)
        {
            return Task.FromResult(false);
        }

        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<bool>(cancellationToken);
        }

        var registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        if (!Post(() =>
            {
                try
                {
                    completion.TrySetResult(action());
                }
                catch (Exception)
                {
                    completion.TrySetResult(false);
                    RefreshAndPublish(SystemActivityServiceState.PartiallyAvailable);
                }
                finally
                {
                    registration.Dispose();
                }
            }))
        {
            registration.Dispose();
            completion.TrySetResult(false);
        }

        return completion.Task;
    }

    private bool Post(Action action)
    {
        if (_disposed || _workItems.IsAddingCompleted)
        {
            return false;
        }

        try
        {
            return _workItems.TryAdd(action);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void RequestRefresh() => _refreshScheduler.Request();

    private void PostRebindAudio() => _rebindScheduler.Request();

    private void OnMediaSnapshotChanged(object? sender, MediaSnapshot snapshot)
    {
        var nextSourceId = ResolveSelectedMediaSourceId();
        var previousSourceId = Interlocked.Exchange(ref _selectedMediaSourceId, nextSourceId);
        if (!string.Equals(previousSourceId, nextSourceId, StringComparison.OrdinalIgnoreCase))
        {
            RequestRefresh();
        }
    }

    private void OnMediaStateChanged(object? sender, MediaServiceState state)
    {
        Interlocked.Exchange(ref _selectedMediaSourceId, ResolveSelectedMediaSourceId());
        RequestRefresh();
    }

    private string? ResolveSelectedMediaSourceId() =>
        !string.IsNullOrWhiteSpace(_media.Selection.SelectedSourceId)
            ? _media.Selection.SelectedSourceId
            : _media.Current.Source.Id;

    private void OnCameraAdded(DeviceWatcher sender, DeviceInformation device) => Post(() =>
    {
        _cameraDeviceIds.Add(device.Id);
        RefreshAndPublish();
    });

    private void OnCameraRemoved(DeviceWatcher sender, DeviceInformationUpdate device) => Post(() =>
    {
        _cameraDeviceIds.Remove(device.Id);
        RefreshAndPublish();
    });

    private void OnCameraEnumerationCompleted(DeviceWatcher sender, object args) => Post(RefreshAndPublish);

    private void OnCameraAccessChanged(AppCapability sender, AppCapabilityAccessChangedEventArgs args) =>
        Post(RefreshAndPublish);

    private static T Activate<T>(IMMDevice device, Guid interfaceId) where T : class
    {
        CoreAudioNative.ThrowIfFailed(device.Activate(
            ref interfaceId, ClsContext.All, 0, out var activated));
        return (T)activated;
    }

    private int GetDefaultEndpoint(
        AudioDataFlow flow,
        AudioDeviceRole preferredRole,
        AudioDeviceRole fallbackRole,
        out IMMDevice device)
    {
        var result = _deviceEnumerator!.GetDefaultAudioEndpoint(flow, preferredRole, out device);
        return result >= 0 || preferredRole == fallbackRole
            ? result
            : _deviceEnumerator.GetDefaultAudioEndpoint(flow, fallbackRole, out device);
    }

    private void CleanupAudioBindings()
    {
        foreach (var session in _sessions)
        {
            session.Dispose();
        }

        _sessions.Clear();
        _applicationSession = null;

        if (_renderSessionManager is not null && _renderSessionNotification is not null)
        {
            _renderSessionManager.UnregisterSessionNotification(_renderSessionNotification);
        }

        if (_captureSessionManager is not null && _captureSessionNotification is not null)
        {
            _captureSessionManager.UnregisterSessionNotification(_captureSessionNotification);
        }

        if (_endpointVolume is not null && _endpointVolumeCallback is not null)
        {
            _endpointVolume.UnregisterControlChangeNotify(_endpointVolumeCallback);
        }

        ReleaseComObject(_renderSessionManager);
        ReleaseComObject(_captureSessionManager);
        ReleaseComObject(_endpointVolume);
        ReleaseComObject(_renderDevice);
        ReleaseComObject(_captureDevice);
        _renderSessionManager = null;
        _captureSessionManager = null;
        _endpointVolume = null;
        _renderDevice = null;
        _captureDevice = null;
        _renderSessionNotification = null;
        _captureSessionNotification = null;
        _endpointVolumeCallback = null;
    }

    private void CleanupAudio()
    {
        CleanupAudioBindings();
        if (_deviceEnumerator is not null && _deviceNotification is not null)
        {
            _deviceEnumerator.UnregisterEndpointNotificationCallback(_deviceNotification);
        }

        ReleaseComObject(_deviceEnumerator);
        _deviceEnumerator = null;
        _deviceNotification = null;
    }

    private void CleanupCamera()
    {
        if (_cameraWatcher is not null)
        {
            _cameraWatcher.Added -= OnCameraAdded;
            _cameraWatcher.Removed -= OnCameraRemoved;
            _cameraWatcher.EnumerationCompleted -= OnCameraEnumerationCompleted;
            if (_cameraWatcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
            {
                _cameraWatcher.Stop();
            }

            _cameraWatcher = null;
        }

        if (_cameraCapability is not null)
        {
            _cameraCapability.AccessChanged -= OnCameraAccessChanged;
            _cameraCapability = null;
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            try
            {
                Marshal.FinalReleaseComObject(value);
            }
            catch (InvalidComObjectException)
            {
            }
        }
    }

    [ComVisible(true)]
    private sealed class EndpointVolumeCallback(Action refreshRequested) : IAudioEndpointVolumeCallback
    {
        public int OnNotify(nint notificationData)
        {
            refreshRequested();
            return 0;
        }
    }

    [ComVisible(true)]
    private sealed class EndpointNotificationCallback(Action rebindRequested) : IMMNotificationClient
    {
        public int OnDeviceStateChanged(string deviceId, uint newState)
        {
            rebindRequested();
            return 0;
        }

        public int OnDeviceAdded(string deviceId)
        {
            rebindRequested();
            return 0;
        }

        public int OnDeviceRemoved(string deviceId)
        {
            rebindRequested();
            return 0;
        }

        public int OnDefaultDeviceChanged(AudioDataFlow flow, AudioDeviceRole role, string? defaultDeviceId)
        {
            rebindRequested();
            return 0;
        }

        public int OnPropertyValueChanged(string deviceId, PropertyKey key) => 0;
    }

    [ComVisible(true)]
    private sealed class SessionNotificationCallback(Action rebindRequested) : IAudioSessionNotification
    {
        public int OnSessionCreated(IAudioSessionControl newSession)
        {
            rebindRequested();
            return 0;
        }
    }
}
