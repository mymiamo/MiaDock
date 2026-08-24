using MiaDock.Modules.Media.Models;
using MiaDock.Platform.Windows.Audio;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class MediaAudioCaptureDecisionTests
{
    [TestMethod]
    public void Select_UsesFreshEnabledSystemSpectrumOtherwiseSessionPeak()
    {
        var spectrum = new MediaAudioLevelSnapshot(
            MediaAudioLevelSource.SystemSpectrum, 0.9, 0.8, 0.7, 0, 0);
        var session = MediaAudioLevelSnapshot.FromSessionPeak(0.4, 0.3);

        Assert.AreEqual(spectrum, MediaAudioCaptureDecision.Select(true, spectrum, true, session));
        Assert.AreEqual(session, MediaAudioCaptureDecision.Select(false, spectrum, true, session));
        Assert.AreEqual(session, MediaAudioCaptureDecision.Select(true, spectrum, false, session));
        Assert.AreEqual(MediaAudioLevelSource.Unavailable,
            MediaAudioCaptureDecision.Select(
                true,
                spectrum,
                false,
                MediaAudioLevelSnapshot.Silent).Source);
    }

    [TestMethod]
    public void SessionPeak_DoesNotClaimFrequencySpectrum()
    {
        var snapshot = MediaAudioLevelSnapshot.FromSessionPeak(0.8, 0.5);
        Assert.AreEqual(MediaAudioLevelSource.SessionPeak, snapshot.Source);
        Assert.IsFalse(snapshot.HasFrequencySpectrum);
        Assert.AreEqual(0.8, snapshot.Amplitude, 0.0001);
    }
}
