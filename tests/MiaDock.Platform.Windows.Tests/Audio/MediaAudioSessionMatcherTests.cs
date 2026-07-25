using MiaDock.Platform.Windows.Audio;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class MediaAudioSessionMatcherTests
{
    [TestMethod]
    [DataRow("Spotify.exe", "Spotify", true)]
    [DataRow("Chrome.exe", "chrome", true)]
    [DataRow("AppleMusicWin.exe", "AppleMusicWin", true)]
    [DataRow("AppleInc.AppleMusicWin_nzyj5cx40ttqa!App", "AppleMusic", true)]
    [DataRow("SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", "Spotify", true)]
    [DataRow("Microsoft.MicrosoftEdge_8wekyb3d8bbwe!MicrosoftEdge", "msedge", true)]
    [DataRow("Spotify.exe", "msedge", false)]
    [DataRow("AppleInc.AppleMusicWin_nzyj5cx40ttqa!App", "ApplicationFrameHost", false)]
    [DataRow(null, "Spotify", false)]
    public void IsMatch_UsesNormalizedSourceAndProcessNames(
        string? sourceId,
        string? processName,
        bool expected) =>
        Assert.AreEqual(expected, MediaAudioSessionMatcher.IsMatch(sourceId, processName));
}
