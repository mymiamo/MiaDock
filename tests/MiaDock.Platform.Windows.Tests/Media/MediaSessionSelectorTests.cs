using MiaDock.Modules.Media.Models;
using MiaDock.Platform.Windows.Media;

namespace MiaDock.Platform.Windows.Tests.Media;

[TestClass]
public sealed class MediaSessionSelectorTests
{
    [TestMethod]
    public void SelectedSourceOnly_WhenSourceIsMissing_ReturnsNull()
    {
        var sessions = new[] { Create("browser", "browser", PlaybackStatus.Playing) };
        var selection = new MediaSelectionOptions(
            "spotify",
            MediaFallbackBehavior.SelectedSourceOnly);

        var result = MediaSessionSelector.Select(sessions, selection);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void Fallback_WhenSourceIsMissing_PrefersPlayingSession()
    {
        var sessions = new[]
        {
            Create("paused", "browser", PlaybackStatus.Paused, isSystemCurrent: true),
            Create("playing", "apple", PlaybackStatus.Playing)
        };
        var selection = new MediaSelectionOptions(
            "spotify",
            MediaFallbackBehavior.UseAnotherActiveSession);

        var result = MediaSessionSelector.Select(sessions, selection);

        Assert.AreEqual("playing", result?.SessionKey);
    }

    [TestMethod]
    public void MultipleSessionsForSelectedSource_PrefersPlayingSession()
    {
        var sessions = new[]
        {
            Create("old", "browser", PlaybackStatus.Paused, isSystemCurrent: true),
            Create("youtube", "browser", PlaybackStatus.Playing)
        };
        var selection = new MediaSelectionOptions(
            "browser",
            MediaFallbackBehavior.SelectedSourceOnly);

        var result = MediaSessionSelector.Select(sessions, selection);

        Assert.AreEqual("youtube", result?.SessionKey);
    }

    private static MediaSessionDescriptor Create(
        string key,
        string source,
        PlaybackStatus status,
        bool isSystemCurrent = false) =>
        new(key, source, status, isSystemCurrent, DateTimeOffset.UtcNow);
}
