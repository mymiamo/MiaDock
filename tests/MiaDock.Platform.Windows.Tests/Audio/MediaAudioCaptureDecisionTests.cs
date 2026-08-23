using MiaDock.Modules.Media.Models;
using MiaDock.Platform.Windows.Audio;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class MediaAudioCaptureDecisionTests
{
    [TestMethod]
    public void Select_PrefersProcessThenEndpointThenSessionPeak()
    {
        var process = Spectrum(MediaAudioLevelSource.ProcessSpectrum, 0.9);
        var endpoint = Spectrum(MediaAudioLevelSource.EndpointSpectrum, 0.7);
        var session = MediaAudioLevelSnapshot.FromSessionPeak(0.4, 0.3);

        Assert.AreEqual(process, MediaAudioCaptureDecision.Select(process, true, endpoint, true, session));
        Assert.AreEqual(endpoint, MediaAudioCaptureDecision.Select(process, false, endpoint, true, session));
        Assert.AreEqual(session, MediaAudioCaptureDecision.Select(process, false, endpoint, false, session));
        Assert.AreEqual(MediaAudioLevelSource.Unavailable,
            MediaAudioCaptureDecision.Select(process, false, endpoint, false, MediaAudioLevelSnapshot.Silent).Source);
    }

    [TestMethod]
    public void SessionPeak_DoesNotClaimFrequencySpectrum()
    {
        var snapshot = MediaAudioLevelSnapshot.FromSessionPeak(0.8, 0.5);

        Assert.AreEqual(MediaAudioLevelSource.SessionPeak, snapshot.Source);
        Assert.IsFalse(snapshot.HasFrequencySpectrum);
        Assert.AreEqual(0.8, snapshot.Amplitude, 0.0001);
    }

    [TestMethod]
    public void CandidateSelection_SkipsActiveButSilentPidAndUsesNextCandidate()
    {
        var unhealthy = new Dictionary<uint, long> { [100] = 6000 };
        var selected = ProcessLoopbackHealth.SelectCandidate(
            [(100u, 0.42f), (200u, 0.38f)],
            unhealthy,
            1000);

        Assert.IsTrue(ProcessLoopbackHealth.ShouldReject(0.42, 600, 30, 0));
        Assert.AreEqual(200u, selected);
    }

    private static MediaAudioLevelSnapshot Spectrum(MediaAudioLevelSource source, double level) =>
        new(source, level, level, level, 0, 0);
}
