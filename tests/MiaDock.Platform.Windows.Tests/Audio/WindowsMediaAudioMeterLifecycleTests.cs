using System.Runtime.InteropServices;
using MiaDock.Modules.Media.Models;
using MiaDock.Platform.Windows.Audio;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class WindowsMediaAudioMeterLifecycleTests
{
    [TestMethod]
    public void PlaybackGate_TransientSessionHandoff_KeepsSpectrumRunning()
    {
        var gate = new SpectrumPlaybackGate();
        const long startedAt = 10_000;

        Assert.IsTrue(gate.Evaluate(true, true, true, PlaybackStatus.Playing, false, startedAt));
        Assert.IsTrue(gate.Evaluate(true, true, false, PlaybackStatus.Stopped, false, startedAt + 100));
        Assert.IsTrue(gate.Evaluate(true, true, true, PlaybackStatus.Playing, false, startedAt + 600));
        Assert.IsTrue(gate.Evaluate(
            true,
            true,
            false,
            PlaybackStatus.Stopped,
            false,
            startedAt + SpectrumPlaybackGate.StopGraceMilliseconds + 500));
    }

    [TestMethod]
    public void PlaybackGate_StablePause_StopsAfterGracePeriod()
    {
        var gate = new SpectrumPlaybackGate();
        const long startedAt = 20_000;
        var pauseAt = startedAt + 100;

        Assert.IsTrue(gate.Evaluate(true, true, true, PlaybackStatus.Playing, false, startedAt));
        Assert.IsTrue(gate.Evaluate(true, true, true, PlaybackStatus.Paused, false, pauseAt));
        Assert.IsTrue(gate.Evaluate(
            true,
            true,
            true,
            PlaybackStatus.Paused,
            false,
            pauseAt + SpectrumPlaybackGate.StopGraceMilliseconds - 1));
        Assert.IsFalse(gate.Evaluate(
            true,
            true,
            true,
            PlaybackStatus.Paused,
            false,
            pauseAt + SpectrumPlaybackGate.StopGraceMilliseconds));
    }

    [TestMethod]
    public void PlaybackGate_MisreportedPauseWithOutputSignal_KeepsSpectrumRunning()
    {
        var gate = new SpectrumPlaybackGate();

        Assert.IsTrue(gate.Evaluate(true, true, true, PlaybackStatus.Playing, false, 100));
        Assert.IsTrue(gate.Evaluate(true, true, true, PlaybackStatus.Paused, true, 10_000));
        Assert.IsTrue(gate.Evaluate(true, true, true, PlaybackStatus.Paused, true, 20_000));
    }

    [TestMethod]
    public void PlaybackGate_DisableOrInactive_StopsImmediately()
    {
        var gate = new SpectrumPlaybackGate();

        Assert.IsTrue(gate.Evaluate(true, true, true, PlaybackStatus.Playing, false, 100));
        Assert.IsFalse(gate.Evaluate(false, true, true, PlaybackStatus.Playing, true, 101));
        Assert.IsFalse(gate.Evaluate(true, false, true, PlaybackStatus.Playing, true, 102));
    }

    [TestMethod]
    public void ChannelPeakBuffer_IsMarshaledAsCallerAllocatedFloatArray()
    {
        var method = typeof(IAudioMeterInformation).GetMethod(
            nameof(IAudioMeterInformation.GetChannelsPeakValues));
        Assert.IsNotNull(method);
        var bufferParameter = method.GetParameters()[1];
        var marshalAs = bufferParameter
            .GetCustomAttributes(typeof(MarshalAsAttribute), inherit: false)
            .Cast<MarshalAsAttribute>()
            .SingleOrDefault();

        Assert.IsNotNull(marshalAs);
        Assert.AreEqual(UnmanagedType.LPArray, marshalAs.Value);
        Assert.AreEqual(UnmanagedType.R4, marshalAs.ArraySubType);
        Assert.AreEqual(0, marshalAs.SizeParamIndex);
        Assert.IsTrue(bufferParameter.IsOut);
    }
}
