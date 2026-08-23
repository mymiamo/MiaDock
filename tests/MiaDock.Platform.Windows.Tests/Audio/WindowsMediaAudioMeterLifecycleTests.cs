using System.Runtime.InteropServices;
using MiaDock.Platform.Windows.Audio;

namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class WindowsMediaAudioMeterLifecycleTests
{
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

    [TestMethod]
    public void WorkerCleanup_GuardsDetachedComObjectsAndDisposedShutdown()
    {
        var sourcePath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MiaDock.Platform.Windows",
            "Audio",
            "WindowsMediaAudioMeterService.cs");
        var source = File.ReadAllText(sourcePath);

        StringAssert.Contains(source, "catch (Exception)");
        StringAssert.Contains(source, "TryCleanupAudio();");
        StringAssert.Contains(source, "InvalidComObjectException");
        StringAssert.Contains(source, "MediaAudioMeterFailed");
        Assert.IsFalse(source.Contains("FinalReleaseComObject", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains(
            "catch (Exception) when (!_disposed)",
            StringComparison.Ordinal));

        var systemActivitySource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MiaDock.Platform.Windows",
            "Audio",
            "WindowsSystemActivityService.cs"));
        var sessionHandleSource = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MiaDock.Platform.Windows",
            "Audio",
            "AudioSessionHandle.cs"));
        Assert.DoesNotContain("FinalReleaseComObject", systemActivitySource, StringComparison.Ordinal);
        Assert.DoesNotContain("FinalReleaseComObject", sessionHandleSource, StringComparison.Ordinal);
        StringAssert.Contains(systemActivitySource, "AudioTopologyRebind");
        StringAssert.Contains(systemActivitySource, "AudioTopologyDebounceInterval");
        StringAssert.Contains(systemActivitySource, "_audioRebindTimer.Change(");
        Assert.IsFalse(systemActivitySource.Contains(
            "FlushAsync(timeout.Token)",
            StringComparison.Ordinal));
    }

    [TestMethod]
    public void WorkerSamplesAtThirtyFramesPerSecondWithoutChangingAudioSessionBinding()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MiaDock.Platform.Windows",
            "Audio",
            "WindowsMediaAudioMeterService.cs"));

        StringAssert.Contains(source, "SampleIntervalMilliseconds = 33");
        StringAssert.Contains(source, "TimeSpan.FromMilliseconds(SampleIntervalMilliseconds)");
        StringAssert.Contains(source, "MediaAudioSessionMatcher.IsMatch(sourceId, processName, processPath)");
    }

    [TestMethod]
    public void WorkerFallsBackToTheSessionPeakWhenPerChannelMeteringIsUnavailable()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MiaDock.Platform.Windows",
            "Audio",
            "WindowsMediaAudioMeterService.cs"));

        StringAssert.Contains(source, "TryReadChannelPeaks(meter, channelPeaks");
        StringAssert.Contains(source, "meter.GetPeakValue(out var peak)");
        StringAssert.Contains(source, "The caller will use GetPeakValue instead.");
    }

    [TestMethod]
    public void WorkerCapturesTheSelectedMediaProcessAndFeedsFrequencySpectrum()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MiaDock.Platform.Windows",
            "Audio",
            "WindowsMediaAudioMeterService.cs"));

        StringAssert.Contains(source, "WithProcessLoopback(");
        StringAssert.Contains(source, "ProcessLoopbackMode.IncludeTargetProcessTree");
        StringAssert.Contains(source, "AudioSpectrumAnalyzer");
        StringAssert.Contains(source, "WithBufferLength(40)");
        StringAssert.Contains(source, "Media spectrum data is available.");
        StringAssert.Contains(source, "Media spectrum contains visual activity.");
        StringAssert.Contains(source, "Publish(ReadSpectrumOrSessionPeak(leftPeak, rightPeak));");
        StringAssert.Contains(source, "_smoother.Update(leftPeak, rightPeak)");
        StringAssert.Contains(source, "real Core Audio session-peak fallback");
        StringAssert.Contains(source, "CoreAudioNative.MeterInformationId");
        StringAssert.Contains(source, "Math.Max(leftPeak, rightPeak) <= 0.001f");
        StringAssert.Contains(source, "_endpointMeter.GetPeakValue(out var endpointPeak)");
        StringAssert.Contains(source, "AudioSpectrumAnalyzer.MinimumScale + 0.02");
        StringAssert.Contains(source, "spectrumHasActivity");
        StringAssert.Contains(source, "Media spectrum loopback stopped unexpectedly; rebinding.");
        Assert.DoesNotContain("args.Exception is null", source, StringComparison.Ordinal);
        StringAssert.Contains(source, "RequestRebind();");
        Assert.DoesNotContain("GateBand(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadSpectrum(Math.Max(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("WasapiLoopbackCapture", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MiaDock.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
