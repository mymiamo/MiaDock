using MiaDock.Platform.Windows.Audio;
using MiaDock.Modules.Media.Models;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class AudioLevelSmootherTests
{
    [TestMethod]
    public void Update_TracksRealPeakWithinVisualBounds()
    {
        var smoother = new AudioLevelSmoother();
        var quiet = smoother.Update(0.05);
        var loud = smoother.Update(0.9);

        Assert.IsGreaterThan(quiet.Amplitude, loud.Amplitude);
        Assert.AreEqual(MediaAudioLevelSource.SessionPeak, loud.Source);
        Assert.IsFalse(loud.HasFrequencySpectrum);
        Assert.IsGreaterThanOrEqualTo(0, loud.LeftAmplitude);
        Assert.IsLessThanOrEqualTo(1, loud.LeftAmplitude);
        Assert.IsLessThanOrEqualTo(1, loud.RightAmplitude);
    }

    [TestMethod]
    public void Reset_ReturnsSilentMinimum()
    {
        var smoother = new AudioLevelSmoother();
        _ = smoother.Update(1);

        var reset = smoother.Reset();

        Assert.IsFalse(reset.IsAvailable);
        Assert.AreEqual(MediaAudioLevelSource.Unavailable, reset.Source);
        Assert.AreEqual(0, reset.LeftAmplitude, 0.0001);
        Assert.AreEqual(0, reset.RightAmplitude, 0.0001);
    }

    [TestMethod]
    public void Update_TracksRealLeftAndRightChannelsIndependently()
    {
        var smoother = new AudioLevelSmoother();

        var leftHeavy = smoother.Update(0.95, 0.02);

        Assert.IsGreaterThan(leftHeavy.RightAmplitude, leftHeavy.LeftAmplitude);
        Assert.AreEqual(0.18, leftHeavy.Treble, 0.0001);
        Assert.AreEqual(0.18, leftHeavy.Bass, 0.0001);
        Assert.AreEqual(0.18, leftHeavy.Vocal, 0.0001);
    }
}
