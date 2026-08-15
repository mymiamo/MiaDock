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
