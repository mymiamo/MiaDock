using MiaDock.Modules.Media.Models;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class TrackIdentityTests
{
    [TestMethod]
    public void From_IgnoresPlaybackAndTimelineFields()
    {
        var snapshot = CreateSnapshot();
        var changedTimeline = snapshot with
        {
            PlaybackStatus = PlaybackStatus.Paused,
            Position = TimeSpan.FromSeconds(90),
            Volume = 0.1
        };

        Assert.AreEqual(TrackIdentity.From(snapshot), TrackIdentity.From(changedTimeline));
    }

    [TestMethod]
    public void From_DetectsTrackAndSourceChanges()
    {
        var snapshot = CreateSnapshot();
        var changedTrack = snapshot with
        {
            Track = snapshot.Track with { Title = "Next track" }
        };
        var changedSource = snapshot with
        {
            Source = snapshot.Source with { Id = "other.player" }
        };

        Assert.AreNotEqual(TrackIdentity.From(snapshot), TrackIdentity.From(changedTrack));
        Assert.AreNotEqual(TrackIdentity.From(snapshot), TrackIdentity.From(changedSource));
    }

    [TestMethod]
    public void From_DetectsAlbumMetadataCompletion()
    {
        var snapshot = CreateSnapshot();
        var completedAlbumMetadata = snapshot with
        {
            Track = snapshot.Track with { AlbumTitle = "Updated album metadata" }
        };

        Assert.AreNotEqual(TrackIdentity.From(snapshot), TrackIdentity.From(completedAlbumMetadata));
    }

    [TestMethod]
    public void From_IgnoresArtworkRefreshRevision()
    {
        var snapshot = CreateSnapshot() with { TrackRevision = 10 };
        var refreshedArtwork = snapshot with { TrackRevision = 11 };

        Assert.AreEqual(TrackIdentity.From(snapshot), TrackIdentity.From(refreshedArtwork));
    }

    [TestMethod]
    public void From_EmptySnapshot_ReturnsNull()
    {
        Assert.IsNull(TrackIdentity.From(MediaSnapshot.Empty));
    }

    private static MediaSnapshot CreateSnapshot() => new(
        new MediaSourceInfo("player", "Player", null),
        new TrackInfo("Track", "Artist", "Album", null),
        PlaybackStatus.Playing,
        1,
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMinutes(3),
        0.5,
        new MediaCapabilities(true, true, true, true, true, false));
}
