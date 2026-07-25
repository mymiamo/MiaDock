using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiaDock.Core.Threading;
using MiaDock.Modules.Media.Models;
using MiaDock.Modules.Media.Services;

namespace MiaDock.Modules.Media.ViewModels;

public sealed partial class MusicModuleViewModel : ObservableObject, IDisposable
{
    private readonly IMediaSessionService _mediaService;
    private readonly IMediaAudioMeterService? _audioMeter;
    private readonly IUiDispatcher _dispatcher;
    private CancellationTokenSource? _timelineCancellation;
    private TimeSpan _displayedPosition;
    private long _lastAppliedSequence;
    private long _timelineGeneration;
    private bool _isDisposed;
    private readonly HashSet<object> _audioMeterConsumers = [];
    private readonly object _audioLevelSync = new();
    private readonly object _snapshotSync = new();
    private MediaAudioLevelSnapshot _pendingAudioLevel = MediaAudioLevelSnapshot.Silent;
    private MediaSnapshot _pendingSnapshot;
    private long _pendingAudioLevelVersion;
    private long _pendingSnapshotVersion;
    private int _audioLevelDispatchPending;
    private int _snapshotDispatchPending;
    private bool _legacyAudioMeterRequested;

    public event EventHandler<TrackIdentity>? TrackChanged;

    public MusicModuleViewModel(
        IMediaSessionService mediaService,
        IUiDispatcher? dispatcher = null,
        IMediaAudioMeterService? audioMeter = null)
    {
        _mediaService = mediaService;
        _dispatcher = dispatcher ?? ImmediateUiDispatcher.Instance;
        _audioMeter = audioMeter;
        _current = mediaService.Current;
        _pendingSnapshot = _current;
        _lastAppliedSequence = _current.Sequence;
        _sources = mediaService.Sources;
        _serviceState = mediaService.State;
        _displayedPosition = Current.Position;
        _mediaService.SnapshotChanged += OnSnapshotChanged;
        _mediaService.SourcesChanged += OnSourcesChanged;
        _mediaService.StateChanged += OnStateChanged;
        if (_audioMeter is not null)
        {
            _audioMeter.LevelChanged += OnAudioLevelChanged;
            ApplyAudioLevel(_audioMeter.Current);
        }
        RestartTimelineTicker();
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    [NotifyCanExecuteChangedFor(nameof(SeekCommand))]
    private MediaSnapshot _current;

    [ObservableProperty]
    private IReadOnlyList<MediaSourceInfo> _sources;

    [ObservableProperty]
    private MediaServiceState _serviceState;

    public bool IsMediaAvailable => Current.HasMedia;

    public string PositionText => FormatTime(_displayedPosition);

    public string RemainingText => $"-{FormatTime(Current.Duration - _displayedPosition)}";

    public string PlaybackGlyph => Current.PlaybackStatus == PlaybackStatus.Playing ? "\uE769" : "\uE768";

    public double ProgressPercent => Current.Duration <= TimeSpan.Zero
        ? 0
        : Math.Clamp(
            _displayedPosition.TotalMilliseconds / Current.Duration.TotalMilliseconds * 100,
            0,
            100);

    public double VolumePercent => Current.Volume * 100;

    public double LeftAudioLevel { get; private set; } = 0.18;

    public double CenterAudioLevel { get; private set; } = 0.18;

    public double RightAudioLevel { get; private set; } = 0.18;

    public bool IsAudioLevelAvailable { get; private set; }

    public bool HasAudioActivity => Current.PlaybackStatus == PlaybackStatus.Playing ||
        IsAudioLevelAvailable && Math.Max(LeftAudioLevel, Math.Max(CenterAudioLevel, RightAudioLevel)) > 0.205;

    public void SetAudioMeterActive(bool active)
    {
        _legacyAudioMeterRequested = active;
        UpdateAudioMeterActivity();
    }

    public void SetAudioMeterActive(object consumer, bool active)
    {
        ArgumentNullException.ThrowIfNull(consumer);

        if (active)
        {
            _audioMeterConsumers.Add(consumer);
        }
        else
        {
            _audioMeterConsumers.Remove(consumer);
        }

        UpdateAudioMeterActivity();
    }

    [RelayCommand(CanExecute = nameof(CanSkipPrevious))]
    private async Task PreviousAsync() => await _mediaService.SkipPreviousAsync();

    [RelayCommand(CanExecute = nameof(CanTogglePlayback))]
    private async Task PlayPauseAsync() => await _mediaService.TogglePlaybackAsync();

    [RelayCommand(CanExecute = nameof(CanSkipNext))]
    private async Task NextAsync() => await _mediaService.SkipNextAsync();

    [RelayCommand(CanExecute = nameof(CanSeek))]
    private async Task SeekAsync(double progressPercent)
    {
        var normalizedProgress = Math.Clamp(progressPercent / 100, 0, 1);
        await _mediaService.SeekAsync(Current.Duration * normalizedProgress);
    }

    private bool CanSkipPrevious() => Current.Capabilities.CanSkipPrevious;

    private bool CanTogglePlayback() => Current.PlaybackStatus == PlaybackStatus.Playing
        ? Current.Capabilities.CanPause
        : Current.Capabilities.CanPlay;

    private bool CanSkipNext() => Current.Capabilities.CanSkipNext;

    private bool CanSeek() => Current.Capabilities.CanSeek && Current.Duration > TimeSpan.Zero;

    private void OnSnapshotChanged(object? sender, MediaSnapshot snapshot)
    {
        lock (_snapshotSync)
        {
            _pendingSnapshot = snapshot;
            _pendingSnapshotVersion++;
        }

        QueueSnapshotDispatch();
    }

    private void QueueSnapshotDispatch()
    {
        if (_isDisposed ||
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
        MediaSnapshot snapshot;
        long version;
        lock (_snapshotSync)
        {
            snapshot = _pendingSnapshot;
            version = _pendingSnapshotVersion;
        }

        if (!_isDisposed)
        {
            ApplySnapshot(snapshot);
        }

        Volatile.Write(ref _snapshotDispatchPending, 0);
        lock (_snapshotSync)
        {
            if (_isDisposed || version == _pendingSnapshotVersion)
            {
                return;
            }
        }

        QueueSnapshotDispatch();
    }

    private void OnSourcesChanged(object? sender, IReadOnlyList<MediaSourceInfo> sources) =>
        Dispatch(() => Sources = sources);

    private void OnStateChanged(object? sender, MediaServiceState state) =>
        Dispatch(() => ServiceState = state);

    private void ApplySnapshot(MediaSnapshot snapshot)
    {
        if (snapshot.Sequence > 0 && snapshot.Sequence <= _lastAppliedSequence)
        {
            return;
        }

        if (snapshot.Sequence > 0)
        {
            _lastAppliedSequence = snapshot.Sequence;
        }

        var previous = Current;
        var previousTrack = TrackIdentity.From(previous);
        var currentTrack = TrackIdentity.From(snapshot);
        var restartTimeline =
            previous.PlaybackStatus != snapshot.PlaybackStatus ||
            previous.PlaybackRate != snapshot.PlaybackRate ||
            previous.Duration != snapshot.Duration ||
            previous.TrackRevision != snapshot.TrackRevision;
        Current = snapshot;
        _displayedPosition = snapshot.Position;
        NotifyTimelineProperties();
        OnPropertyChanged(nameof(PlaybackGlyph));
        OnPropertyChanged(nameof(IsMediaAvailable));
        OnPropertyChanged(nameof(VolumePercent));
        OnPropertyChanged(nameof(HasAudioActivity));
        if (restartTimeline)
        {
            RestartTimelineTicker();
        }
        UpdateAudioMeterActivity();

        if (currentTrack is { } identity && identity != previousTrack)
        {
            TrackChanged?.Invoke(this, identity);
        }
    }

    private void OnAudioLevelChanged(object? sender, MediaAudioLevelSnapshot snapshot)
    {
        lock (_audioLevelSync)
        {
            _pendingAudioLevel = snapshot;
            _pendingAudioLevelVersion++;
        }

        QueueAudioLevelDispatch();
    }

    private void QueueAudioLevelDispatch()
    {
        if (_isDisposed ||
            Interlocked.CompareExchange(ref _audioLevelDispatchPending, 1, 0) != 0)
        {
            return;
        }

        if (_dispatcher.HasThreadAccess)
        {
            DrainLatestAudioLevel();
            return;
        }

        if (!_dispatcher.TryEnqueue(DrainLatestAudioLevel))
        {
            Volatile.Write(ref _audioLevelDispatchPending, 0);
        }
    }

    private void DrainLatestAudioLevel()
    {
        MediaAudioLevelSnapshot snapshot;
        long version;
        lock (_audioLevelSync)
        {
            snapshot = _pendingAudioLevel;
            version = _pendingAudioLevelVersion;
        }

        if (!_isDisposed)
        {
            ApplyAudioLevel(snapshot);
        }

        Volatile.Write(ref _audioLevelDispatchPending, 0);
        lock (_audioLevelSync)
        {
            if (_isDisposed || version == _pendingAudioLevelVersion)
            {
                return;
            }
        }

        QueueAudioLevelDispatch();
    }

    private void ApplyAudioLevel(MediaAudioLevelSnapshot snapshot)
    {
        IsAudioLevelAvailable = snapshot.IsAvailable;
        LeftAudioLevel = snapshot.Left;
        CenterAudioLevel = snapshot.Center;
        RightAudioLevel = snapshot.Right;
        OnPropertyChanged(nameof(LeftAudioLevel));
        OnPropertyChanged(nameof(CenterAudioLevel));
        OnPropertyChanged(nameof(RightAudioLevel));
        OnPropertyChanged(nameof(IsAudioLevelAvailable));
        OnPropertyChanged(nameof(HasAudioActivity));
    }

    private void UpdateAudioMeterActivity() =>
        _audioMeter?.SetActive(
            (_legacyAudioMeterRequested || _audioMeterConsumers.Count > 0) && Current.HasMedia);

    private void RestartTimelineTicker()
    {
        var generation = Interlocked.Increment(ref _timelineGeneration);
        _timelineCancellation?.Cancel();
        _timelineCancellation?.Dispose();
        _timelineCancellation = null;

        if (_isDisposed || Current.PlaybackStatus != PlaybackStatus.Playing || Current.Duration <= TimeSpan.Zero)
        {
            return;
        }

        _timelineCancellation = new CancellationTokenSource();
        _ = RunTimelineTickerAsync(generation, _timelineCancellation.Token);
    }

    private async Task RunTimelineTickerAsync(long generation, CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                Dispatch(() =>
                {
                    if (_isDisposed || generation != Interlocked.Read(ref _timelineGeneration))
                    {
                        return;
                    }

                    var increment = TimeSpan.FromSeconds(Math.Max(Current.PlaybackRate, 0));
                    _displayedPosition = _displayedPosition + increment > Current.Duration
                        ? Current.Duration
                        : _displayedPosition + increment;
                    NotifyTimelineProperties();
                });
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void NotifyTimelineProperties()
    {
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(ProgressPercent));
    }

    private void Dispatch(Action callback)
    {
        if (_dispatcher.HasThreadAccess)
        {
            callback();
            return;
        }

        _dispatcher.TryEnqueue(callback);
    }

    private static string FormatTime(TimeSpan value)
    {
        var safeValue = value < TimeSpan.Zero ? TimeSpan.Zero : value;
        return safeValue.TotalHours >= 1
            ? safeValue.ToString(@"h\:mm\:ss")
            : safeValue.ToString(@"m\:ss");
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        Interlocked.Increment(ref _timelineGeneration);
        _timelineCancellation?.Cancel();
        _timelineCancellation?.Dispose();
        _timelineCancellation = null;
        _mediaService.SnapshotChanged -= OnSnapshotChanged;
        _mediaService.SourcesChanged -= OnSourcesChanged;
        _mediaService.StateChanged -= OnStateChanged;
        if (_audioMeter is not null)
        {
            _audioMeterConsumers.Clear();
            _legacyAudioMeterRequested = false;
            _audioMeter.LevelChanged -= OnAudioLevelChanged;
            _audioMeter.SetActive(false);
        }
    }

    private sealed class ImmediateUiDispatcher : IUiDispatcher
    {
        public static ImmediateUiDispatcher Instance { get; } = new();

        public bool HasThreadAccess => true;

        public bool TryEnqueue(Action callback)
        {
            callback();
            return true;
        }
    }
}
