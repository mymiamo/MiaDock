using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Media.Control;
using MiaDock.Modules.Media.Models;
using MiaDock.Modules.Media.Services;
using MiaDock.Core.Logging;

namespace MiaDock.Platform.Windows.Media;

public sealed class WindowsMediaSessionService : IMediaSessionService
{
    private const int AccessDeniedHResult = unchecked((int)0x80070005);
    private const string SessionManagerTypeName =
        "Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager";

    private readonly object _sync = new();
    private readonly SemaphoreSlim _initializeGate = new(1, 1);
    private readonly WindowsAppIdentityResolver _identityResolver;
    private readonly WindowsMediaMapper _mapper;
    private readonly ILogService? _log;
    private readonly CoalescingRefreshQueue _topologyQueue;
    private readonly CoalescingRefreshQueue _snapshotQueue;
    private readonly GenerationSessionAccessCoordinator<GlobalSystemMediaTransportControlsSession> _sessionAccess = new();
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _selectedSession;
    private GenerationSessionAccessCoordinator<GlobalSystemMediaTransportControlsSession>.SessionLease? _selectedLease;
    private MediaSourceInfo? _selectedSource;
    private CancellationTokenSource? _metadataValidationCancellation;
    private long _refreshGeneration;
    private long _topologyGeneration;
    private long _trackRevision;
    private long _artworkAttemptedRevision = -1;
    private long _publishedSequence;
    private long _diagnosticSnapshotLeaseGeneration = -1;
    private bool _isDisposed;

    public WindowsMediaSessionService(MediaImageCache imageCache, ILogService? log = null)
    {
        _log = log;
        var imageReader = new MediaImageReader(imageCache);
        _identityResolver = new WindowsAppIdentityResolver(imageReader);
        _mapper = new WindowsMediaMapper(imageReader);
        _topologyQueue = new CoalescingRefreshQueue(RebuildSessionsAsync);
        _snapshotQueue = new CoalescingRefreshQueue(
            RefreshSnapshotAsync,
            TimeSpan.FromMilliseconds(200));
    }

    public event EventHandler<MediaSnapshot>? SnapshotChanged;

    public event EventHandler<IReadOnlyList<MediaSourceInfo>>? SourcesChanged;

    public event EventHandler<MediaServiceState>? StateChanged;

    public MediaServiceState State { get; private set; } = MediaServiceState.NotInitialized;

    public IReadOnlyList<MediaSourceInfo> Sources { get; private set; } = [];

    public MediaSnapshot Current { get; private set; } = MediaSnapshot.Empty;

    public MediaSelectionOptions Selection { get; private set; } = MediaSelectionOptions.FollowSystemCurrent;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await _initializeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (State is MediaServiceState.Ready or MediaServiceState.Initializing)
            {
                return;
            }

            SetState(MediaServiceState.Initializing);
            if (!ApiInformation.IsTypePresent(SessionManagerTypeName))
            {
                SetState(MediaServiceState.Unavailable);
                return;
            }

            try
            {
                var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                cancellationToken.ThrowIfCancellationRequested();
                lock (_sync)
                {
                    _manager = manager;
                    manager.SessionsChanged += OnSessionsChanged;
                    manager.CurrentSessionChanged += OnCurrentSessionChanged;
                }

                SetState(MediaServiceState.Ready);
                _topologyQueue.Request();
                await _topologyQueue.WaitForIdleAsync().ConfigureAwait(false);
                await _snapshotQueue.WaitForIdleAsync().ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException exception)
            {
                LogFailure("initialize", exception);
                SetState(MediaServiceState.AccessDenied);
            }
            catch (COMException exception) when (exception.HResult == AccessDeniedHResult)
            {
                LogFailure("initialize", exception);
                SetState(MediaServiceState.AccessDenied);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                LogFailure("initialize", exception);
                SetState(MediaServiceState.Faulted);
            }
        }
        finally
        {
            _initializeGate.Release();
        }
    }

    public async Task SetSelectionAsync(
        MediaSelectionOptions selection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ThrowIfDisposed();
        Selection = selection;
        if (State == MediaServiceState.Ready)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _topologyQueue.Request();
            await _topologyQueue.WaitForIdleAsync().ConfigureAwait(false);
            await _snapshotQueue.WaitForIdleAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public async Task<bool> TogglePlaybackAsync(CancellationToken cancellationToken = default)
    {
        var lease = _sessionAccess.Capture();
        if (lease is null)
        {
            return false;
        }

        try
        {
            var succeeded = await _sessionAccess.ExecuteAsync(
                lease,
                async (session, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    var playbackInfo = session.GetPlaybackInfo();
                    var controls = playbackInfo.Controls;
                    IAsyncOperation<bool>? command = controls.IsPlayPauseToggleEnabled
                        ? session.TryTogglePlayPauseAsync()
                        : playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing && controls.IsPauseEnabled
                            ? session.TryPauseAsync()
                            : controls.IsPlayEnabled
                                ? session.TryPlayAsync()
                                : null;
                    return command is not null && await command.AsTask(token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            if (succeeded)
            {
                _snapshotQueue.Request();
            }

            return succeeded;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogTransportFailure("toggle-playback", exception, lease.Generation);
            return false;
        }
    }

    public Task<bool> SkipPreviousAsync(CancellationToken cancellationToken = default) =>
        ExecuteTransportCommandAsync(
            controls => controls.IsPreviousEnabled,
            session => session.TrySkipPreviousAsync(),
            cancellationToken);

    public Task<bool> SkipNextAsync(CancellationToken cancellationToken = default) =>
        ExecuteTransportCommandAsync(
            controls => controls.IsNextEnabled,
            session => session.TrySkipNextAsync(),
            cancellationToken);

    public async Task<bool> SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        var lease = _sessionAccess.Capture();
        if (lease is null)
        {
            return false;
        }

        try
        {
            var succeeded = await _sessionAccess.ExecuteAsync(
                lease,
                async (session, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    var playbackInfo = session.GetPlaybackInfo();
                    if (!playbackInfo.Controls.IsPlaybackPositionEnabled) return false;
                    var timeline = session.GetTimelineProperties();
                    var target = timeline.StartTime + (position < TimeSpan.Zero ? TimeSpan.Zero : position);
                    var minimum = timeline.MinSeekTime;
                    var maximum = timeline.MaxSeekTime > minimum ? timeline.MaxSeekTime : timeline.EndTime;
                    if (target < minimum) target = minimum;
                    if (maximum > minimum && target > maximum) target = maximum;
                    return await session.TryChangePlaybackPositionAsync(target.Ticks)
                        .AsTask(token)
                        .ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            if (succeeded)
            {
                _snapshotQueue.Request();
            }

            return succeeded;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogTransportFailure("seek", exception, lease.Generation);
            return false;
        }
    }

    private async Task<bool> ExecuteTransportCommandAsync(
        Func<GlobalSystemMediaTransportControlsSessionPlaybackControls, bool> isEnabled,
        Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> execute,
        CancellationToken cancellationToken)
    {
        var lease = _sessionAccess.Capture();
        if (lease is null)
        {
            return false;
        }

        try
        {
            var succeeded = await _sessionAccess.ExecuteAsync(
                lease,
                async (session, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    if (!isEnabled(session.GetPlaybackInfo().Controls)) return false;
                    return await execute(session).AsTask(token).ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            if (succeeded)
            {
                _snapshotQueue.Request();
            }

            return succeeded;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            LogTransportFailure("transport-command", exception, lease.Generation);
            return false;
        }
    }

    private async Task RebuildSessionsAsync(CancellationToken cancellationToken)
    {
        GlobalSystemMediaTransportControlsSessionManager? manager;
        long topologyGeneration;
        lock (_sync)
        {
            manager = _manager;
            topologyGeneration = _topologyGeneration;
        }

        if (manager is null)
        {
            return;
        }

        GlobalSystemMediaTransportControlsSession[] sessions;
        GlobalSystemMediaTransportControlsSession? systemCurrent;
        try
        {
            sessions = manager.GetSessions().ToArray();
            systemCurrent = manager.GetCurrentSession();
        }
        catch (Exception exception)
        {
            LogFailure("copy-topology", exception, topologyGeneration);
            return;
        }

        var sessionLookup = new Dictionary<string, GlobalSystemMediaTransportControlsSession>(StringComparer.Ordinal);
        var descriptors = new List<MediaSessionDescriptor>(sessions.Length);
        foreach (var session in sessions)
        {
            try
            {
                var sessionKey = CreateSessionKey(session);
                sessionLookup[sessionKey] = session;
                descriptors.Add(CreateDescriptor(sessionKey, session, systemCurrent));
            }
            catch
            {
                // The session vanished while the native topology was being copied.
            }
        }

        var resolvedSources = new Dictionary<string, MediaSourceInfo>(StringComparer.Ordinal);
        foreach (var sourceId in descriptors
                     .Select(item => item.SourceId)
                     .Where(item => !string.IsNullOrWhiteSpace(item))
                     .Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            resolvedSources[sourceId] = await _identityResolver.ResolveAsync(
                sourceId,
                cancellationToken).ConfigureAwait(false);
            lock (_sync)
            {
                if (_isDisposed ||
                    topologyGeneration != _topologyGeneration ||
                    !ReferenceEquals(manager, _manager))
                {
                    return;
                }
            }
        }

        var selectedDescriptor = MediaSessionSelector.Select(descriptors, Selection);
        var selectedSession = selectedDescriptor is null ? null : sessionLookup[selectedDescriptor.SessionKey];
        var selectedSource = selectedDescriptor is null
            ? null
            : resolvedSources.GetValueOrDefault(selectedDescriptor.SourceId);
        var sessionChanged = SwitchSelectedSession(selectedSession, selectedSource);

        Sources = resolvedSources.Values
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        SourcesChanged?.Invoke(this, Sources);

        _log?.Write(
            TechnicalLogLevel.Information,
            TechnicalEventIds.MediaTopologyRebuilt,
            "Media",
            "Media session topology was rebuilt.",
            properties: new Dictionary<string, object?>
            {
                ["topologyGeneration"] = topologyGeneration,
                ["sessionCount"] = sessions.Length,
                ["count"] = Sources.Count,
                ["selected"] = selectedSession is not null,
                ["state"] = sessionChanged ? "session-changed" : "session-unchanged"
            });

        if (sessionChanged)
        {
            _log?.Write(
                TechnicalLogLevel.Information,
                TechnicalEventIds.MediaSessionChanged,
                "Media",
                "The selected Windows media session changed.",
                properties: new Dictionary<string, object?>
                {
                    ["generation"] = _selectedLease?.Generation ?? 0,
                    ["selected"] = selectedSession is not null,
                    ["trackRevision"] = _trackRevision
                });
            await FlushDiagnosticCheckpointAsync().ConfigureAwait(false);
        }

        if (selectedSession is null || selectedSource is null)
        {
            SetCurrent(MediaSnapshot.Empty);
        }
        else
        {
            await RefreshSnapshotAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RefreshSnapshotAsync(CancellationToken cancellationToken)
    {
        GenerationSessionAccessCoordinator<GlobalSystemMediaTransportControlsSession>.SessionLease? lease;
        MediaSourceInfo? source;
        long generation;
        long trackRevision;
        lock (_sync)
        {
            lease = _selectedLease;
            source = _selectedSource;
            generation = _refreshGeneration;
            trackRevision = _trackRevision;
        }

        if (lease is null || source is null)
        {
            SetCurrent(MediaSnapshot.Empty);
            return;
        }

        if (Interlocked.Exchange(ref _diagnosticSnapshotLeaseGeneration, lease.Generation) != lease.Generation)
        {
            _log?.Write(
                TechnicalLogLevel.Information,
                TechnicalEventIds.MediaSnapshotStarted,
                "Media",
                "The first snapshot read for the selected media session is starting.",
                properties: new Dictionary<string, object?>
                {
                    ["phase"] = "before-native-read",
                    ["generation"] = lease.Generation,
                    ["trackRevision"] = trackRevision
                });
            await FlushDiagnosticCheckpointAsync().ConfigureAwait(false);
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            var snapshot = await _sessionAccess.ExecuteAsync(
                lease,
                (session, token) => _mapper.MapAsync(
                    session,
                    source,
                    token,
                    includeArtwork: false),
                cancellationToken).ConfigureAwait(false);
            EventHandler<MediaSnapshot>? changed;
            lock (_sync)
            {
                if (!ReferenceEquals(lease, _selectedLease) ||
                    !_sessionAccess.IsCurrent(lease) ||
                    generation != _refreshGeneration ||
                    trackRevision != _trackRevision ||
                    _isDisposed)
                {
                    return;
                }

                if (trackRevision == Current.TrackRevision &&
                    TrackIdentity.From(Current) == TrackIdentity.From(snapshot))
                {
                    snapshot = snapshot with
                    {
                        Track = snapshot.Track with { Artwork = Current.Track.Artwork }
                    };
                }

                snapshot = Stamp(snapshot, trackRevision);
                Current = snapshot;
                changed = SnapshotChanged;
            }

            changed?.Invoke(this, snapshot);

            var shouldRefreshArtwork = false;
            lock (_sync)
            {
                if (ReferenceEquals(lease, _selectedLease) &&
                    _sessionAccess.IsCurrent(lease) &&
                    snapshot.Track.Artwork is null &&
                    snapshot.HasMedia &&
                    _artworkAttemptedRevision != trackRevision)
                {
                    _artworkAttemptedRevision = trackRevision;
                    shouldRefreshArtwork = true;
                }
            }

            if (shouldRefreshArtwork)
            {
                await RefreshArtworkAsync(
                    lease,
                    source,
                    TrackIdentity.From(snapshot),
                    trackRevision,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The selected session changed. The retired generation is discarded.
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _log?.Write(
                TechnicalLogLevel.Warning,
                TechnicalEventIds.MediaSnapshotFailed,
                "Media",
                "A media snapshot read failed; the topology will be refreshed.",
                exception,
                new Dictionary<string, object?>
                {
                    ["phase"] = "snapshot-read",
                    ["generation"] = lease.Generation,
                    ["trackRevision"] = trackRevision,
                    ["durationMs"] = Stopwatch.GetElapsedTime(started).TotalMilliseconds
                });
            _topologyQueue.Request();
        }
    }

    private async Task RefreshArtworkAsync(
        GenerationSessionAccessCoordinator<GlobalSystemMediaTransportControlsSession>.SessionLease lease,
        MediaSourceInfo source,
        TrackIdentity? expectedIdentity,
        long expectedTrackRevision,
        CancellationToken cancellationToken)
    {
        if (expectedIdentity is null)
        {
            return;
        }

        MediaSnapshot mapped;
        try
        {
            mapped = await _sessionAccess.ExecuteAsync(
                lease,
                (session, token) => _mapper.MapAsync(
                    session,
                    source,
                    token,
                    includeArtwork: true,
                    artworkRevision: expectedTrackRevision),
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return;
        }
        if (mapped.Track.Artwork is null)
        {
            return;
        }

        MediaSnapshot updated;
        lock (_sync)
        {
            if (!ReferenceEquals(lease, _selectedLease) ||
                !_sessionAccess.IsCurrent(lease) ||
                expectedTrackRevision != _trackRevision ||
                TrackIdentity.From(Current) != expectedIdentity ||
                TrackIdentity.From(mapped) != expectedIdentity)
            {
                return;
            }

            updated = Current with
            {
                Track = Current.Track with { Artwork = mapped.Track.Artwork },
                Sequence = Interlocked.Increment(ref _publishedSequence)
            };
        }

        SetCurrent(updated);
    }

    private MediaSnapshot Stamp(MediaSnapshot snapshot, long trackRevision) => snapshot with
    {
        Sequence = Interlocked.Increment(ref _publishedSequence),
        TrackRevision = trackRevision
    };

    private bool SwitchSelectedSession(
        GlobalSystemMediaTransportControlsSession? session,
        MediaSourceInfo? source)
    {
        CancellationTokenSource? previousValidation = null;
        var sessionChanged = false;
        lock (_sync)
        {
            if (IsSameSession(_selectedSession, session))
            {
                _selectedSource = source;
                return false;
            }

            if (_selectedSession is not null)
            {
                DetachSessionEvents(_selectedSession);
            }

            _selectedSession = session;
            _selectedSource = source;
            _selectedLease = _sessionAccess.Switch(session);
            previousValidation = _metadataValidationCancellation;
            _metadataValidationCancellation = null;
            _refreshGeneration++;
            _trackRevision++;
            sessionChanged = true;
            if (_selectedSession is not null)
            {
                AttachSessionEvents(_selectedSession);
            }
        }

        previousValidation?.Cancel();
        previousValidation?.Dispose();
        if (sessionChanged)
        {
            SetCurrent(MediaSnapshot.Empty);
        }

        return sessionChanged;
    }

    private static MediaSessionDescriptor CreateDescriptor(
        string sessionKey,
        GlobalSystemMediaTransportControlsSession session,
        GlobalSystemMediaTransportControlsSession? systemCurrent)
    {
        PlaybackStatus status;
        DateTimeOffset lastUpdated;
        try
        {
            status = WindowsMediaMapper.MapPlaybackStatus(session.GetPlaybackInfo().PlaybackStatus);
            lastUpdated = session.GetTimelineProperties().LastUpdatedTime;
        }
        catch
        {
            status = PlaybackStatus.Stopped;
            lastUpdated = DateTimeOffset.MinValue;
        }

        return new MediaSessionDescriptor(
            sessionKey,
            session.SourceAppUserModelId,
            status,
            IsSameSession(session, systemCurrent),
            lastUpdated);
    }

    private static string CreateSessionKey(GlobalSystemMediaTransportControlsSession session) =>
        $"{session.SourceAppUserModelId}:{RuntimeHelpers.GetHashCode(session)}";

    private static bool IsSameSession(
        GlobalSystemMediaTransportControlsSession? left,
        GlobalSystemMediaTransportControlsSession? right) =>
        ReferenceEquals(left, right);

    private void AttachSessionEvents(GlobalSystemMediaTransportControlsSession session)
    {
        try { session.MediaPropertiesChanged += OnMediaPropertiesChanged; } catch { }
        try { session.PlaybackInfoChanged += OnPlaybackInfoChanged; } catch { }
        try { session.TimelinePropertiesChanged += OnTimelinePropertiesChanged; } catch { }
    }

    private void DetachSessionEvents(GlobalSystemMediaTransportControlsSession session)
    {
        try { session.MediaPropertiesChanged -= OnMediaPropertiesChanged; } catch { }
        try { session.PlaybackInfoChanged -= OnPlaybackInfoChanged; } catch { }
        try { session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged; } catch { }
    }

    private void SetCurrent(MediaSnapshot snapshot)
    {
        EventHandler<MediaSnapshot>? changed;
        lock (_sync)
        {
            if (_isDisposed ||
                snapshot.Sequence > 0 && Current.Sequence >= snapshot.Sequence)
            {
                return;
            }

            Current = snapshot;
            changed = SnapshotChanged;
        }

        changed?.Invoke(this, snapshot);
    }

    private void SetState(MediaServiceState state)
    {
        var previous = State;
        State = state;
        _log?.Write(
            state is MediaServiceState.Faulted or MediaServiceState.AccessDenied
                ? TechnicalLogLevel.Warning
                : TechnicalLogLevel.Information,
            TechnicalEventIds.MediaServiceStateChanged,
            "Media",
            "The Windows media service state changed.",
            properties: new Dictionary<string, object?>
            {
                ["state"] = state.ToString(),
                ["reason"] = previous.ToString()
            });
        StateChanged?.Invoke(this, state);
    }

    private void LogFailure(string phase, Exception exception, long generation = 0) =>
        _log?.Write(
            TechnicalLogLevel.Warning,
            TechnicalEventIds.MediaSnapshotFailed,
            "Media",
            "A Windows media operation failed safely.",
            exception,
            new Dictionary<string, object?>
            {
                ["phase"] = phase,
                ["generation"] = generation,
                ["hresult"] = exception.HResult
            });

    private void LogTransportFailure(string operation, Exception exception, long generation) =>
        _log?.Write(
            TechnicalLogLevel.Warning,
            TechnicalEventIds.MediaTransportFailed,
            "Media",
            "A media transport command failed safely.",
            exception,
            new Dictionary<string, object?>
            {
                ["operation"] = operation,
                ["generation"] = generation,
                ["hresult"] = exception.HResult
            });

    private async Task FlushDiagnosticCheckpointAsync()
    {
        if (_log is null)
        {
            return;
        }

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await _log.FlushAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OperationCanceledException or IOException)
        {
            // Diagnostics must never block media recovery.
        }
    }

    private void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args) => RequestTopologyRefresh(sender);

    private void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args) => RequestTopologyRefresh(sender);

    private void RequestTopologyRefresh(GlobalSystemMediaTransportControlsSessionManager sender)
    {
        lock (_sync)
        {
            if (_isDisposed || !ReferenceEquals(sender, _manager))
            {
                return;
            }
            _topologyGeneration++;
        }
        _topologyQueue.Request();
    }

    private void OnMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args)
    {
        MediaSnapshot? invalidatedArtwork = null;
        lock (_sync)
        {
            if (_isDisposed || !IsSameSession(sender, _selectedSession))
            {
                return;
            }

            _refreshGeneration++;
            _trackRevision++;
            if (Current.HasMedia)
            {
                invalidatedArtwork = Current with
                {
                    Track = Current.Track with { Artwork = null },
                    TrackRevision = _trackRevision,
                    Sequence = Interlocked.Increment(ref _publishedSequence)
                };
            }
        }

        if (invalidatedArtwork is not null)
        {
            SetCurrent(invalidatedArtwork);
        }

        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }
        }

        _snapshotQueue.Request();
        ScheduleMetadataValidation();
    }

    private void OnPlaybackInfoChanged(
        GlobalSystemMediaTransportControlsSession sender,
        PlaybackInfoChangedEventArgs args) => RequestSnapshotRefresh(sender);

    private void OnTimelinePropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        TimelinePropertiesChangedEventArgs args) => RequestSnapshotRefresh(sender);

    private void RequestSnapshotRefresh(GlobalSystemMediaTransportControlsSession sender)
    {
        lock (_sync)
        {
            if (_isDisposed || !IsSameSession(sender, _selectedSession))
            {
                return;
            }
        }

        _snapshotQueue.Request();
    }

    private void ScheduleMetadataValidation()
    {
        var next = new CancellationTokenSource();
        CancellationTokenSource? previous;
        lock (_sync)
        {
            if (_isDisposed)
            {
                next.Dispose();
                return;
            }

            previous = _metadataValidationCancellation;
            _metadataValidationCancellation = next;
        }

        previous?.Cancel();
        previous?.Dispose();
        _ = ValidateMetadataAsync(next.Token);
    }

    private async Task ValidateMetadataAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var delay in new[] { 100, 250, 500, 1000, 2000 })
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                lock (_sync)
                {
                    if (_isDisposed || _selectedSession is null)
                    {
                        return;
                    }

                    _refreshGeneration++;
                }

                _snapshotQueue.Request();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_isDisposed, this);

    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource? metadataValidation;
        GenerationSessionAccessCoordinator<GlobalSystemMediaTransportControlsSession>.SessionLease? drainingLease;
        lock (_sync)
        {
            if (_isDisposed)
            {
                return;
            }

            // 1) Stop callbacks. 2) Retire the lease so in-flight work cancels.
            // 3) Drain queues/native calls before the process tears down COM.
            _isDisposed = true;
            metadataValidation = _metadataValidationCancellation;
            _metadataValidationCancellation = null;
            if (_manager is not null)
            {
                try { _manager.SessionsChanged -= OnSessionsChanged; } catch { }
                try { _manager.CurrentSessionChanged -= OnCurrentSessionChanged; } catch { }
            }

            if (_selectedSession is not null)
            {
                DetachSessionEvents(_selectedSession);
            }

            drainingLease = _selectedLease;
            _selectedLease = _sessionAccess.Switch(null);
            _manager = null;
            _selectedSession = null;
            _selectedSource = null;
        }

        metadataValidation?.Cancel();
        metadataValidation?.Dispose();
        await _topologyQueue.DisposeAsync().ConfigureAwait(false);
        await _snapshotQueue.DisposeAsync().ConfigureAwait(false);
        if (drainingLease is not null)
        {
            await drainingLease.WaitForIdleAsync().ConfigureAwait(false);
        }

        await _sessionAccess.DisposeAsync().ConfigureAwait(false);
        _initializeGate.Dispose();
    }
}
