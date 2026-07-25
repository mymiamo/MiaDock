using MiaDock.Modules.Media.Services;
using MiaDock.Modules.Media.ViewModels;
using MiaDock.Modules.Media.Models;
using MiaDock.Core.Threading;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class MusicModuleViewModelTests
{
    [TestMethod]
    public void LimitedScenario_DisablesUnsupportedCommands()
    {
        var service = new FakeMediaService();
        service.SelectScenario("limited-controls");
        using var viewModel = new MusicModuleViewModel(service);

        Assert.IsFalse(viewModel.PreviousCommand.CanExecute(null));
        Assert.IsFalse(viewModel.NextCommand.CanExecute(null));
        Assert.IsTrue(viewModel.PlayPauseCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task SeekCommand_SeeksFakeMedia()
    {
        var service = new FakeMediaService();
        using var viewModel = new MusicModuleViewModel(service);

        await viewModel.SeekCommand.ExecuteAsync(50);

        Assert.AreEqual(50, viewModel.ProgressPercent, 0.001);
    }

    [TestMethod]
    public void SnapshotChange_RaisesFormattedPropertyNotifications()
    {
        var service = new FakeMediaService();
        using var viewModel = new MusicModuleViewModel(service);
        var changedProperties = new HashSet<string>();
        viewModel.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName ?? string.Empty);

        service.TogglePlayback();

        Assert.IsTrue(changedProperties.Contains(nameof(viewModel.PlaybackGlyph)));
        Assert.IsTrue(changedProperties.Contains(nameof(viewModel.PositionText)));
    }

    [TestMethod]
    public void SnapshotChange_FromWorkerContext_IsDispatched()
    {
        var service = new FakeMediaService();
        var dispatcher = new QueuedDispatcher();
        using var viewModel = new MusicModuleViewModel(service, dispatcher);

        service.TogglePlayback();

        Assert.AreEqual(PlaybackStatus.Playing, viewModel.Current.PlaybackStatus);
        dispatcher.RunQueued();
        Assert.AreEqual(PlaybackStatus.Paused, viewModel.Current.PlaybackStatus);
    }

    [TestMethod]
    public void PlaybackAndTimelineChanges_DoNotRaiseTrackChanged()
    {
        var service = new FakeMediaService();
        using var viewModel = new MusicModuleViewModel(service);
        var trackChangeCount = 0;
        viewModel.TrackChanged += (_, _) => trackChangeCount++;

        service.TogglePlayback();
        service.Seek(0.25);

        Assert.AreEqual(0, trackChangeCount);
    }

    [TestMethod]
    public void NewTrack_RaisesTrackChangedOnce()
    {
        var service = new FakeMediaService();
        using var viewModel = new MusicModuleViewModel(service);
        var trackChangeCount = 0;
        viewModel.TrackChanged += (_, _) => trackChangeCount++;

        service.SkipNext();

        Assert.AreEqual(1, trackChangeCount);
    }

    [TestMethod]
    public void OlderSnapshot_CannotRestorePreviousTrack()
    {
        var oldTrack = SequencedMediaService.CreateSnapshot("Eski parça", 8);
        var service = new SequencedMediaService(SequencedMediaService.CreateSnapshot("Yeni parça", 10));
        using var viewModel = new MusicModuleViewModel(service);

        service.Publish(oldTrack);

        Assert.AreEqual("Yeni parça", viewModel.Current.Track.Title);
        Assert.AreEqual(10, viewModel.Current.Sequence);
    }

    [TestMethod]
    public void RapidTrackChanges_KeepNewestMetadataAndArtworkRevision()
    {
        var service = new SequencedMediaService(SequencedMediaService.CreateSnapshot("Başlangıç", 1));
        using var viewModel = new MusicModuleViewModel(service);

        for (var sequence = 2; sequence <= 21; sequence++)
        {
            service.Publish(SequencedMediaService.CreateSnapshot($"Parça {sequence}", sequence));
        }

        service.Publish(SequencedMediaService.CreateSnapshot("Gecikmiş parça", 12));

        Assert.AreEqual("Parça 21", viewModel.Current.Track.Title);
        Assert.AreEqual(21, viewModel.Current.TrackRevision);
        Assert.AreEqual(21, viewModel.Current.Sequence);
    }

    [TestMethod]
    public void SameTitleWithNewRevision_RefreshesArtworkWithoutDuplicateTrackEvent()
    {
        var first = SequencedMediaService.CreateSnapshot("Aynı başlık", 1);
        var service = new SequencedMediaService(first);
        using var viewModel = new MusicModuleViewModel(service);
        var trackChangeCount = 0;
        viewModel.TrackChanged += (_, _) => trackChangeCount++;

        var refreshed = first with
        {
            Sequence = 2,
            TrackRevision = 2,
            Track = first.Track with
            {
                Artwork = MediaImage.FromBytes("revision-2", [1, 2, 3], "image/png")
            }
        };
        service.Publish(refreshed);

        Assert.AreEqual(0, trackChangeCount);
        Assert.AreEqual("revision-2", viewModel.Current.Track.Artwork?.CacheKey);
    }

    [TestMethod]
    public void AudioMeter_RemainsActiveWhenMediaExistsButPlaybackStatusIsPaused()
    {
        var snapshot = SequencedMediaService.CreateSnapshot("Parça", 1) with
        {
            PlaybackStatus = PlaybackStatus.Paused
        };
        var service = new SequencedMediaService(snapshot);
        using var meter = new RecordingAudioMeter();
        using var viewModel = new MusicModuleViewModel(service, audioMeter: meter);

        viewModel.SetAudioMeterActive(true);
        meter.Publish(new MediaAudioLevelSnapshot(true, 0.8, 0.9, 0.4));

        Assert.IsTrue(meter.IsActive);
        Assert.AreEqual(0.8, viewModel.LeftAudioLevel, 0.0001);
        Assert.AreEqual(0.9, viewModel.CenterAudioLevel, 0.0001);
        Assert.AreEqual(0.4, viewModel.RightAudioLevel, 0.0001);
        Assert.IsTrue(viewModel.IsAudioLevelAvailable);
        Assert.IsTrue(viewModel.HasAudioActivity);
    }

    [TestMethod]
    public void AudioMeter_RemainsActiveUntilEveryVisibleConsumerReleasesIt()
    {
        var service = new SequencedMediaService(SequencedMediaService.CreateSnapshot("Parça", 1));
        using var meter = new RecordingAudioMeter();
        using var viewModel = new MusicModuleViewModel(service, audioMeter: meter);
        var clockView = new object();
        var musicView = new object();

        viewModel.SetAudioMeterActive(clockView, true);
        viewModel.SetAudioMeterActive(musicView, true);
        viewModel.SetAudioMeterActive(musicView, false);

        Assert.IsTrue(meter.IsActive);

        viewModel.SetAudioMeterActive(clockView, false);

        Assert.IsFalse(meter.IsActive);
    }

    [TestMethod]
    public void RapidAudioSamples_QueueOneUiCallbackAndApplyNewestSample()
    {
        var service = new SequencedMediaService(SequencedMediaService.CreateSnapshot("Parça", 1));
        var dispatcher = new QueuedDispatcher();
        using var meter = new RecordingAudioMeter();
        using var viewModel = new MusicModuleViewModel(service, dispatcher, meter);

        for (var index = 1; index <= 50_000; index++)
        {
            var level = index / 50_000d;
            meter.Publish(new MediaAudioLevelSnapshot(true, level, level / 2, level / 4));
        }

        Assert.AreEqual(1, dispatcher.PendingCount);
        dispatcher.RunQueued();
        Assert.AreEqual(1, viewModel.LeftAudioLevel, 0.0001);
        Assert.AreEqual(0.5, viewModel.CenterAudioLevel, 0.0001);
        Assert.AreEqual(0.25, viewModel.RightAudioLevel, 0.0001);
        Assert.AreEqual(0, dispatcher.PendingCount);
    }

    [TestMethod]
    public void RapidMediaSnapshots_QueueOneUiCallbackAndApplyNewestSnapshot()
    {
        var initial = SequencedMediaService.CreateSnapshot("İlk", 1);
        var service = new SequencedMediaService(initial);
        var dispatcher = new QueuedDispatcher();
        using var viewModel = new MusicModuleViewModel(service, dispatcher);

        for (var index = 2; index <= 50_001; index++)
        {
            service.Publish(SequencedMediaService.CreateSnapshot($"Parça {index}", index));
        }

        Assert.AreEqual(1, dispatcher.PendingCount);
        dispatcher.RunQueued();
        Assert.AreEqual("Parça 50001", viewModel.Current.Track.Title);
        Assert.AreEqual(50_001, viewModel.Current.Sequence);
        Assert.AreEqual(0, dispatcher.PendingCount);
    }

    private sealed class QueuedDispatcher : IUiDispatcher
    {
        private readonly Queue<Action> _callbacks = new();

        public bool HasThreadAccess => false;
        public int PendingCount => _callbacks.Count;

        public bool TryEnqueue(Action callback)
        {
            _callbacks.Enqueue(callback);
            return true;
        }

        public void RunQueued()
        {
            while (_callbacks.TryDequeue(out var callback))
            {
                callback();
            }
        }
    }

    private sealed class SequencedMediaService(MediaSnapshot current) : IMediaSessionService
    {
        public event EventHandler<MediaSnapshot>? SnapshotChanged;
        public event EventHandler<IReadOnlyList<MediaSourceInfo>>? SourcesChanged { add { } remove { } }
        public event EventHandler<MediaServiceState>? StateChanged { add { } remove { } }
        public MediaServiceState State => MediaServiceState.Ready;
        public IReadOnlyList<MediaSourceInfo> Sources { get; } = [current.Source];
        public MediaSnapshot Current { get; private set; } = current;
        public MediaSelectionOptions Selection { get; } = MediaSelectionOptions.FollowSystemCurrent;
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetSelectionAsync(MediaSelectionOptions selection, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> TogglePlaybackAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> SkipPreviousAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> SkipNextAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> SeekAsync(TimeSpan position, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Publish(MediaSnapshot snapshot)
        {
            Current = snapshot;
            SnapshotChanged?.Invoke(this, snapshot);
        }

        public static MediaSnapshot CreateSnapshot(string title, long sequence) => new(
            new MediaSourceInfo("test", "Test", null),
            new TrackInfo(title, "Sanatçı", "Albüm", null),
            PlaybackStatus.Playing,
            1,
            TimeSpan.Zero,
            TimeSpan.FromMinutes(3),
            0.5,
            new MediaCapabilities(true, true, true, true, true, true))
        {
            Sequence = sequence,
            TrackRevision = sequence
        };
    }

    private sealed class RecordingAudioMeter : IMediaAudioMeterService
    {
        public MediaAudioLevelSnapshot Current { get; private set; } = MediaAudioLevelSnapshot.Silent;
        public bool IsActive { get; private set; }
        public event EventHandler<MediaAudioLevelSnapshot>? LevelChanged;

        public void SetActive(bool active) => IsActive = active;

        public void Publish(MediaAudioLevelSnapshot snapshot)
        {
            Current = snapshot;
            LevelChanged?.Invoke(this, snapshot);
        }

        public void Dispose()
        {
        }
    }
}
