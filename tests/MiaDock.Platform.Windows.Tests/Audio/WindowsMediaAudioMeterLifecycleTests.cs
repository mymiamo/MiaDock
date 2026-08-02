namespace MiaDock.Platform.Windows.Tests.Audio;

[TestClass]
public sealed class WindowsMediaAudioMeterLifecycleTests
{
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
