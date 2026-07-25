using MiaDock.Modules.Media.Models;

namespace MiaDock.Modules.Media.Services;

public sealed class FakeMediaService : IMediaSessionService
{
    private static readonly Uri ArtworkUri = new("ms-appx:///Assets/Mock/album-cover-01.svg");
    private static readonly Uri SourceIconUri = new("ms-appx:///Assets/Mock/source-app-placeholder.svg");

    public FakeMediaService()
    {
        Scenarios = CreateScenarios();
        Current = Scenarios[0].Snapshot;
    }

    public event EventHandler<MediaSnapshot>? SnapshotChanged;

    public event EventHandler<IReadOnlyList<MediaSourceInfo>>? SourcesChanged;

    public event EventHandler<MediaServiceState>? StateChanged;

    public IReadOnlyList<MediaScenario> Scenarios { get; }

    public MediaServiceState State { get; private set; } = MediaServiceState.NotInitialized;

    public IReadOnlyList<MediaSourceInfo> Sources =>
        Scenarios.Select(item => item.Snapshot.Source).DistinctBy(item => item.Id).ToArray();

    public MediaSelectionOptions Selection { get; private set; } = MediaSelectionOptions.FollowSystemCurrent;

    public MediaSnapshot Current { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        State = MediaServiceState.Ready;
        StateChanged?.Invoke(this, State);
        SourcesChanged?.Invoke(this, Sources);
        SnapshotChanged?.Invoke(this, Current);
        return Task.CompletedTask;
    }

    public Task SetSelectionAsync(
        MediaSelectionOptions selection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selection);
        cancellationToken.ThrowIfCancellationRequested();
        Selection = selection;

        if (selection.SelectedSourceId is not null &&
            Sources.All(source => !string.Equals(source.Id, selection.SelectedSourceId, StringComparison.Ordinal)))
        {
            SetCurrent(selection.FallbackBehavior == MediaFallbackBehavior.UseAnotherActiveSession
                ? Scenarios[0].Snapshot
                : MediaSnapshot.Empty);
        }

        return Task.CompletedTask;
    }

    public Task<bool> TogglePlaybackAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var initial = Current.PlaybackStatus;
        TogglePlayback();
        return Task.FromResult(Current.PlaybackStatus != initial);
    }

    public Task<bool> SkipPreviousAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Current.Capabilities.CanSkipPrevious)
        {
            return Task.FromResult(false);
        }

        SkipPrevious();
        return Task.FromResult(true);
    }

    public Task<bool> SkipNextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Current.Capabilities.CanSkipNext)
        {
            return Task.FromResult(false);
        }

        SkipNext();
        return Task.FromResult(true);
    }

    public Task<bool> SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Current.Capabilities.CanSeek || Current.Duration <= TimeSpan.Zero)
        {
            return Task.FromResult(false);
        }

        Seek(position.TotalMilliseconds / Current.Duration.TotalMilliseconds);
        return Task.FromResult(true);
    }

    public void SelectScenario(string scenarioId)
    {
        var scenario = Scenarios.FirstOrDefault(item => item.Id == scenarioId)
            ?? throw new ArgumentException($"Unknown media scenario: {scenarioId}", nameof(scenarioId));

        SetCurrent(scenario.Snapshot);
    }

    public void TogglePlayback()
    {
        var canToggle = Current.PlaybackStatus == PlaybackStatus.Playing
            ? Current.Capabilities.CanPause
            : Current.Capabilities.CanPlay;

        if (!canToggle)
        {
            return;
        }

        var status = Current.PlaybackStatus == PlaybackStatus.Playing
            ? PlaybackStatus.Paused
            : PlaybackStatus.Playing;

        SetCurrent(Current with { PlaybackStatus = status });
    }

    public void SkipPrevious()
    {
        if (!Current.Capabilities.CanSkipPrevious)
        {
            return;
        }

        SetCurrent(Current with { Position = TimeSpan.Zero });
    }

    public void SkipNext()
    {
        if (!Current.Capabilities.CanSkipNext)
        {
            return;
        }

        var index = FindCurrentScenarioIndex();
        SetCurrent(Scenarios[(index + 1) % Scenarios.Count].Snapshot);
    }

    public void Seek(double progress)
    {
        if (!Current.Capabilities.CanSeek)
        {
            return;
        }

        var normalizedProgress = Math.Clamp(progress, 0, 1);
        SetCurrent(Current with { Position = Current.Duration * normalizedProgress });
    }

    public void SetVolume(double volume)
    {
        if (!Current.Capabilities.CanChangeVolume)
        {
            return;
        }

        SetCurrent(Current with { Volume = Math.Clamp(volume, 0, 1) });
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private int FindCurrentScenarioIndex()
    {
        for (var index = 0; index < Scenarios.Count; index++)
        {
            if (Scenarios[index].Snapshot.Track == Current.Track)
            {
                return index;
            }
        }

        return 0;
    }

    private void SetCurrent(MediaSnapshot snapshot)
    {
        Current = snapshot;
        SnapshotChanged?.Invoke(this, snapshot);
    }

    private static IReadOnlyList<MediaScenario> CreateScenarios()
    {
        var source = new MediaSourceInfo(
            "preview.player",
            "Preview Player",
            MediaImage.FromUri("preview.player.icon", SourceIconUri));
        var fullCapabilities = new MediaCapabilities(true, true, true, true, true, true);

        return
        [
            new(
                "normal",
                "Normal track",
                new MediaSnapshot(
                    source,
                    new TrackInfo(
                        "Midnight Signals",
                        "Northbound",
                        "Neon Hours",
                        MediaImage.FromUri("preview.album.normal", ArtworkUri)),
                    PlaybackStatus.Playing,
                    1,
                    TimeSpan.FromSeconds(98),
                    TimeSpan.FromSeconds(244),
                    0.68,
                    fullCapabilities)),
            new(
                "long-text",
                "Long metadata",
                new MediaSnapshot(
                    source,
                    new TrackInfo(
                        "A Very Long Track Title Designed to Verify Trimming Inside the Expanded Island",
                        "An Artist Name That Is Intentionally Longer Than the Available Presentation Area",
                        "Layout Stress Test",
                        MediaImage.FromUri("preview.album.long-text", ArtworkUri)),
                    PlaybackStatus.Paused,
                    1,
                    TimeSpan.FromSeconds(15),
                    TimeSpan.FromSeconds(312),
                    0.45,
                    fullCapabilities)),
            new(
                "missing-artwork",
                "Missing artwork",
                new MediaSnapshot(
                    source,
                    new TrackInfo("Quiet Geometry", "MiaDock Sessions", "Offline", null),
                    PlaybackStatus.Playing,
                    1,
                    TimeSpan.FromSeconds(42),
                    TimeSpan.FromSeconds(183),
                    0.72,
                    fullCapabilities)),
            new(
                "limited-controls",
                "Limited controls",
                new MediaSnapshot(
                    source,
                    new TrackInfo(
                        "Live Stream",
                        "Browser Session",
                        "Live",
                        MediaImage.FromUri("preview.album.limited", ArtworkUri)),
                    PlaybackStatus.Playing,
                    1,
                    TimeSpan.FromMinutes(18),
                    TimeSpan.FromHours(1),
                    0.5,
                    new MediaCapabilities(true, true, false, false, false, true)))
        ];
    }
}
