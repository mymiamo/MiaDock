using MiaDock.Modules.Media.Models;
using MiaDock.Platform.Windows.Audio;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class MediaAudioBindingIdentityTests
{
    [TestMethod]
    public void TimelineAndTrackUpdates_FromSameSource_DoNotRequireRebind()
    {
        var first = CreateSnapshot("source", "Track A", TimeSpan.Zero);
        var updated = CreateSnapshot("source", "Track B", TimeSpan.FromSeconds(30));

        Assert.AreEqual(
            MediaAudioBindingIdentity.From(first),
            MediaAudioBindingIdentity.From(updated));
    }

    [TestMethod]
    public void SourceOrAvailabilityChange_RequiresRebind()
    {
        var first = CreateSnapshot("source-a", "Track", TimeSpan.Zero);
        var anotherSource = CreateSnapshot("source-b", "Track", TimeSpan.Zero);

        Assert.AreNotEqual(
            MediaAudioBindingIdentity.From(first),
            MediaAudioBindingIdentity.From(anotherSource));
        Assert.AreNotEqual(
            MediaAudioBindingIdentity.From(first),
            MediaAudioBindingIdentity.From(MediaSnapshot.Empty));
    }

    private static MediaSnapshot CreateSnapshot(string sourceId, string title, TimeSpan position) => new(
        new MediaSourceInfo(sourceId, sourceId, null),
        new TrackInfo(title, "Artist", "Album", null),
        PlaybackStatus.Playing,
        1,
        position,
        TimeSpan.FromMinutes(3),
        0.5,
        new MediaCapabilities(true, true, true, true, true, true));
}
