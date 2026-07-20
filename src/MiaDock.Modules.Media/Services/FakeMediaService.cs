using MiaDock.Modules.Media.Models;

namespace MiaDock.Modules.Media.Services;

public sealed class FakeMediaService : IFakeMediaService
{
    private static readonly Uri ArtworkUri = new("ms-appx:///Assets/Mock/album-cover-01.svg");
    private static readonly Uri SourceIconUri = new("ms-appx:///Assets/Mock/source-app-placeholder.svg");

    public FakeMediaService()
    {
        Scenarios = CreateScenarios();
        Current = Scenarios[0].Snapshot;
    }

    public event EventHandler<MediaSnapshot>? SnapshotChanged;

    public IReadOnlyList<MediaScenario> Scenarios { get; }

    public MediaSnapshot Current { get; private set; }

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
        var source = new MediaSourceInfo("preview.player", "Preview Player", SourceIconUri);
        var fullCapabilities = new MediaCapabilities(true, true, true, true, true, true);

        return
        [
            new(
                "normal",
                "Normal track",
                new MediaSnapshot(
                    source,
                    new TrackInfo("Midnight Signals", "Northbound", "Neon Hours", ArtworkUri),
                    PlaybackStatus.Playing,
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
                        ArtworkUri),
                    PlaybackStatus.Paused,
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
                    TimeSpan.FromSeconds(42),
                    TimeSpan.FromSeconds(183),
                    0.72,
                    fullCapabilities)),
            new(
                "limited-controls",
                "Limited controls",
                new MediaSnapshot(
                    source,
                    new TrackInfo("Live Stream", "Browser Session", "Live", ArtworkUri),
                    PlaybackStatus.Playing,
                    TimeSpan.FromMinutes(18),
                    TimeSpan.FromHours(1),
                    0.5,
                    new MediaCapabilities(true, true, false, false, false, true)))
        ];
    }
}
