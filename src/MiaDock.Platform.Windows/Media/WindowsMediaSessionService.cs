using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Foundation.Metadata;
using Windows.Media.Control;
using MiaDock.Modules.Media.Models;
using MiaDock.Modules.Media.Services;

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
    private readonly CoalescingRefreshQueue _topologyQueue;
    private readonly CoalescingRefreshQueue _snapshotQueue;
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _selectedSession;
    private MediaSourceInfo? _selectedSource;
    private CancellationTokenSource? _metadataValidationCancellation;
    private long _refreshGeneration;
    private long _trackRevision;
    private long _artworkAttemptedRevision = -1;
    private long _publishedSequence;
    private bool _isDisposed;

    public WindowsMediaSessionService(MediaImageCache imageCache)
    {
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
            catch (UnauthorizedAccessException)
            {
                SetState(MediaServiceState.AccessDenied);
            }
            catch (COMException exception) when (exception.HResult == AccessDeniedHResult)
            {
                SetState(MediaServiceState.AccessDenied);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
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
        var session = GetSelectedSession();
        if (session is null)
        {
            return false;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var playbackInfo = session.GetPlaybackInfo();
            var controls = playbackInfo.Controls;
            bool succeeded;
            if (controls.IsPlayPauseToggleEnabled)
            {
                succeeded = await session.TryTogglePlayPauseAsync();
            }
            else if (playbackInfo.PlaybackStatus ==
                     GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing &&
                     controls.IsPauseEnabled)
            {
                succeeded = await session.TryPauseAsync();
            }
            else if (controls.IsPlayEnabled)
            {
                succeeded = await session.TryPlayAsync();
            }
            else
            {
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (succeeded)
            {
                _snapshotQueue.Request();
            }

            return succeeded;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
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
        var session = GetSelectedSession();
        if (session is null)
        {
            return false;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var playbackInfo = session.GetPlaybackInfo();
            if (!playbackInfo.Controls.IsPlaybackPositionEnabled)
            {
                return false;
            }

            var timeline = session.GetTimelineProperties();
            var target = timeline.StartTime + (position < TimeSpan.Zero ? TimeSpan.Zero : position);
            var minimum = timeline.MinSeekTime;
            var maximum = timeline.MaxSeekTime > minimum ? timeline.MaxSeekTime : timeline.EndTime;
            if (target < minimum)
            {
                target = minimum;
            }

            if (maximum > minimum && target > maximum)
            {
                target = maximum;
            }

            var succeeded = await session.TryChangePlaybackPositionAsync(target.Ticks);
            cancellationToken.ThrowIfCancellationRequested();
            if (succeeded)
            {
                _snapshotQueue.Request();
            }

            return succeeded;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ExecuteTransportCommandAsync(
        Func<GlobalSystemMediaTransportControlsSessionPlaybackControls, bool> isEnabled,
        Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> execute,
        CancellationToken cancellationToken)
    {
        var session = GetSelectedSession();
        if (session is null)
        {
            return false;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!isEnabled(session.GetPlaybackInfo().Controls))
            {
                return false;
            }

            var succeeded = await execute(session);
            cancellationToken.ThrowIfCancellationRequested();
            if (succeeded)
            {
                _snapshotQueue.Request();
            }

            return succeeded;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private async Task RebuildSessionsAsync(CancellationToken cancellationToken)
    {
        GlobalSystemMediaTransportControlsSessionManager? manager;
        lock (_sync)
        {
            manager = _manager;
        }

        if (manager is null)
        {
            return;
        }

        var sessions = manager.GetSessions().ToArray();
        var systemCurrent = manager.GetCurrentSession();
        var resolvedSources = new Dictionary<string, MediaSourceInfo>(StringComparer.Ordinal);
        foreach (var sourceId in sessions
                     .Select(item => item.SourceAppUserModelId)
                     .Where(item => !string.IsNullOrWhiteSpace(item))
                     .Distinct(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            resolvedSources[sourceId] = await _identityResolver.ResolveAsync(
                sourceId,
                cancellationToken).ConfigureAwait(false);
        }

        var sessionLookup = new Dictionary<string, GlobalSystemMediaTransportControlsSession>(StringComparer.Ordinal);
        var descriptors = new List<MediaSessionDescriptor>(sessions.Length);
        foreach (var session in sessions)
        {
            var sessionKey = CreateSessionKey(session);
            sessionLookup[sessionKey] = session;
            descriptors.Add(CreateDescriptor(sessionKey, session, systemCurrent));
        }

        var selectedDescriptor = MediaSessionSelector.Select(descriptors, Selection);
        var selectedSession = selectedDescriptor is null ? null : sessionLookup[selectedDescriptor.SessionKey];
        var selectedSource = selectedDescriptor is null
            ? null
            : resolvedSources.GetValueOrDefault(selectedDescriptor.SourceId);
        SwitchSelectedSession(selectedSession, selectedSource);

        Sources = resolvedSources.Values
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
        SourcesChanged?.Invoke(this, Sources);

        if (selectedSession is null || selectedSource is null)
        {
            SetCurrent(MediaSnapshot.Empty);
        }
        else
        {
            _snapshotQueue.Request();
        }
    }

    private async Task RefreshSnapshotAsync(CancellationToken cancellationToken)
    {
        GlobalSystemMediaTransportControlsSession? session;
        MediaSourceInfo? source;
        long generation;
        long trackRevision;
        lock (_sync)
        {
            session = _selectedSession;
            source = _selectedSource;
            generation = _refreshGeneration;
            trackRevision = _trackRevision;
        }

        if (session is null || source is null)
        {
            SetCurrent(MediaSnapshot.Empty);
            return;
        }

        try
        {
            var snapshot = await _mapper.MapAsync(
                session,
                source,
                cancellationToken,
                includeArtwork: false).ConfigureAwait(false);
            EventHandler<MediaSnapshot>? changed;
            lock (_sync)
            {
                if (!IsSameSession(session, _selectedSession) ||
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
                if (snapshot.Track.Artwork is null &&
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
                    session,
                    source,
                    TrackIdentity.From(snapshot),
                    trackRevision,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            _topologyQueue.Request();
        }
    }

    private async Task RefreshArtworkAsync(
        GlobalSystemMediaTransportControlsSession session,
        MediaSourceInfo source,
        TrackIdentity? expectedIdentity,
        long expectedTrackRevision,
        CancellationToken cancellationToken)
    {
        if (expectedIdentity is null)
        {
            return;
        }

        var mapped = await _mapper.MapAsync(
            session,
            source,
            cancellationToken,
            includeArtwork: true,
            artworkRevision: expectedTrackRevision).ConfigureAwait(false);
        if (mapped.Track.Artwork is null)
        {
            return;
        }

        MediaSnapshot updated;
        lock (_sync)
        {
            if (!IsSameSession(session, _selectedSession) ||
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

    private void SwitchSelectedSession(
        GlobalSystemMediaTransportControlsSession? session,
        MediaSourceInfo? source)
    {
        lock (_sync)
        {
            if (IsSameSession(_selectedSession, session))
            {
                _selectedSource = source;
                return;
            }

            if (_selectedSession is not null)
            {
                _selectedSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _selectedSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                _selectedSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            }

            _selectedSession = session;
            _selectedSource = source;
            _refreshGeneration++;
            _trackRevision++;
            if (_selectedSession is not null)
            {
                _selectedSession.MediaPropertiesChanged += OnMediaPropertiesChanged;
                _selectedSession.PlaybackInfoChanged += OnPlaybackInfoChanged;
                _selectedSession.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
            }
        }
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
        ReferenceEquals(left, right) || (left is not null && left.Equals(right));

    private GlobalSystemMediaTransportControlsSession? GetSelectedSession()
    {
        lock (_sync)
        {
            return _selectedSession;
        }
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
        State = state;
        StateChanged?.Invoke(this, state);
    }

    private void OnSessionsChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        SessionsChangedEventArgs args) => _topologyQueue.Request();

    private void OnCurrentSessionChanged(
        GlobalSystemMediaTransportControlsSessionManager sender,
        CurrentSessionChangedEventArgs args) => _topologyQueue.Request();

    private void OnMediaPropertiesChanged(
        GlobalSystemMediaTransportControlsSession sender,
        MediaPropertiesChangedEventArgs args)
    {
        MediaSnapshot? invalidatedArtwork = null;
        lock (_sync)
        {
            if (!IsSameSession(sender, _selectedSession))
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
            if (!IsSameSession(sender, _selectedSession))
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
        if (_isDisposed)
        {
            return;
        }

        CancellationTokenSource? metadataValidation;
        lock (_sync)
        {
            _isDisposed = true;
            metadataValidation = _metadataValidationCancellation;
            _metadataValidationCancellation = null;
            if (_manager is not null)
            {
                _manager.SessionsChanged -= OnSessionsChanged;
                _manager.CurrentSessionChanged -= OnCurrentSessionChanged;
            }

            if (_selectedSession is not null)
            {
                _selectedSession.MediaPropertiesChanged -= OnMediaPropertiesChanged;
                _selectedSession.PlaybackInfoChanged -= OnPlaybackInfoChanged;
                _selectedSession.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
            }

            _manager = null;
            _selectedSession = null;
            _selectedSource = null;
        }

        metadataValidation?.Cancel();
        metadataValidation?.Dispose();
        await _topologyQueue.DisposeAsync().ConfigureAwait(false);
        await _snapshotQueue.DisposeAsync().ConfigureAwait(false);
        _initializeGate.Dispose();
    }
}
