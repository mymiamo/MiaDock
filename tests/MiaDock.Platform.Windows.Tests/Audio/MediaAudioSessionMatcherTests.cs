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

    [TestMethod]
    public void IsMatch_RecognizesAudioWorkerFromTheSelectedPackagedApp()
    {
        const string sourceId = "AppleInc.AppleMusicWin_nzyj5cx40ttqa!App";
        const string packageWorker =
            @"C:\Program Files\WindowsApps\AppleInc.AppleMusicWin_1.1540.23042.0_x64__nzyj5cx40ttqa\AMPLibraryAgent.exe";
        const string unrelatedWorker =
            @"C:\Program Files\WindowsApps\OtherCompany.OtherApp_1.0.0.0_x64__abcdefghijklm\AudioWorker.exe";

        Assert.IsTrue(MediaAudioSessionMatcher.IsMatch(
            sourceId,
            "AMPLibraryAgent",
            packageWorker));
        Assert.IsFalse(MediaAudioSessionMatcher.IsMatch(
            sourceId,
            "AudioWorker",
            unrelatedWorker));
    }
}
