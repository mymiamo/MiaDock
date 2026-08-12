using MiaDock.Core.Lifecycle;
using MiaDock.Platform.Windows.Lifecycle;
using MiaDock.Platform.Windows.Settings;

namespace MiaDock.Platform.Windows.Tests.Lifecycle;

[TestClass]
public sealed class JsonCrashStateStoreTests
{
    private sealed class TempSettingsPathProvider : ISettingsPathProvider, IDisposable
    {
        private readonly string _directory = Path.Combine(
            Path.GetTempPath(),
            "MiaDockCrashTests",
            Guid.NewGuid().ToString("N"));

        public string GetSettingsFilePath()
        {
            Directory.CreateDirectory(_directory);
            return Path.Combine(_directory, "settings.json");
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_directory))
                {
                    Directory.Delete(_directory, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    [TestMethod]
    public void TryConsumePendingCrash_ReturnsFalse_WhenClean()
    {
        using var paths = new TempSettingsPathProvider();
        var store = new JsonCrashStateStore(paths);
        store.MarkSessionStarted();
        store.MarkCleanShutdown();

        Assert.IsFalse(store.TryConsumePendingCrash(out _));
    }

    [TestMethod]
    public void MarkCrashed_IsConsumedOnce()
    {
        using var paths = new TempSettingsPathProvider();
        var store = new JsonCrashStateStore(paths);
        store.MarkCrashed(new InvalidOperationException("boom"));

        Assert.IsTrue(store.TryConsumePendingCrash(out var record));
        Assert.IsTrue(record.PendingCrash);
        Assert.AreEqual(typeof(InvalidOperationException).FullName, record.ExceptionType);
        Assert.AreEqual("boom", record.ExceptionMessage);
        Assert.IsFalse(store.TryConsumePendingCrash(out _));
    }

    [TestMethod]
    public void UncleanSession_WithoutPendingCrash_DoesNotSurfaceRecoveryUi()
    {
        using var paths = new TempSettingsPathProvider();
        var store = new JsonCrashStateStore(paths);
        store.MarkSessionStarted();

        Assert.IsFalse(store.TryConsumePendingCrash(out _));
        Assert.IsFalse(store.TryConsumePendingCrash(out _));
    }

    [TestMethod]
    public void TryBeginRestart_BlocksAfterMaxRestartsInWindow()
    {
        using var paths = new TempSettingsPathProvider();
        var store = new JsonCrashStateStore(paths);

        Assert.IsTrue(store.TryBeginRestart());
        Assert.IsTrue(store.TryBeginRestart());
        Assert.IsFalse(store.TryBeginRestart());
    }
}
