using MiaDock.Modules.Media.Models;
using MiaDock.Modules.Media.Services;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class FakeMediaServiceTests
{
    [TestMethod]
    public void SelectScenario_UsesRequestedSnapshot()
    {
        var service = new FakeMediaService();

        service.SelectScenario("missing-artwork");

        Assert.AreEqual("Quiet Geometry", service.Current.Track.Title);
        Assert.IsNull(service.Current.Track.ArtworkUri);
    }

    [TestMethod]
    public void SelectScenario_WithUnknownId_Throws()
    {
        var service = new FakeMediaService();

        Assert.ThrowsExactly<ArgumentException>(() => service.SelectScenario("unknown"));
    }

    [TestMethod]
    public void Seek_WhenSupported_ClampsProgress()
    {
        var service = new FakeMediaService();

        service.Seek(2);

        Assert.AreEqual(service.Current.Duration, service.Current.Position);
    }

    [TestMethod]
    public void Seek_WhenUnsupported_DoesNotChangePosition()
    {
        var service = new FakeMediaService();
        service.SelectScenario("limited-controls");
        var initialPosition = service.Current.Position;

        service.Seek(0.9);

        Assert.AreEqual(initialPosition, service.Current.Position);
    }

    [TestMethod]
    public void TogglePlayback_ChangesPlayingToPaused()
    {
        var service = new FakeMediaService();

        service.TogglePlayback();

        Assert.AreEqual(PlaybackStatus.Paused, service.Current.PlaybackStatus);
    }

    [TestMethod]
    public void SetVolume_ClampsToValidRange()
    {
        var service = new FakeMediaService();

        service.SetVolume(-1);

        Assert.AreEqual(0, service.Current.Volume);
    }
}
