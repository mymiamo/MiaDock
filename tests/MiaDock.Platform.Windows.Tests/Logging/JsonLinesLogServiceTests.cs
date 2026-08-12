using System.Diagnostics;
using System.IO.Compression;
using MiaDock.Core.Logging;
using MiaDock.Platform.Windows.Logging;

namespace MiaDock.Platform.Windows.Tests.Logging;

[TestClass]
public sealed class JsonLinesLogServiceTests
{
    private string _directory = null!;

    [TestInitialize]
    public void Initialize() =>
        _directory = Path.Combine(Path.GetTempPath(), "MiaDockTests", Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task WriteAndRead_RoundTripsSafeTechnicalFields()
    {
        await using var service = CreateService();

        service.Write(
            TechnicalLogLevel.Warning,
            "MEDIA_REFRESH",
            "Media",
            "Media state refresh failed.",
            properties: new Dictionary<string, object?> { ["operation"] = "refresh", ["count"] = 2 });

        var entries = await service.ReadLatestAsync();

        Assert.HasCount(1, entries);
        Assert.AreEqual(TechnicalLogLevel.Warning, entries[0].Level);
        Assert.AreEqual("MEDIA_REFRESH", entries[0].EventId);
        var properties = entries[0].Properties;
        Assert.IsNotNull(properties);
        Assert.AreEqual("refresh", properties["operation"]);
        Assert.AreEqual("2", properties["count"]);
        Assert.IsGreaterThan(0, entries[0].SequenceNumber);
        Assert.AreEqual(Environment.ProcessId, entries[0].ProcessId);
        Assert.IsGreaterThan(0, entries[0].ManagedThreadId);
    }

    [TestMethod]
    public async Task LowTraffic_IsPersistedWithoutExplicitFlush()
    {
        await using var service = new JsonLinesLogService(
            new TestLogPathProvider(_directory),
            new SensitiveDataRedactor(),
            flushInterval: TimeSpan.FromMilliseconds(25));

        service.Write(TechnicalLogLevel.Information, "IDLE_FLUSH", "Tests", "Low traffic event.");
        await Task.Delay(150);

        var files = Directory.Exists(_directory)
            ? Directory.GetFiles(_directory, "*.ndjson")
            : Array.Empty<string>();
        Assert.HasCount(1, files);
        StringAssert.Contains(await File.ReadAllTextAsync(files[0]), "IDLE_FLUSH");
    }

    [TestMethod]
    public async Task ExceptionLogging_DoesNotPersistMessageOrPersonalPath()
    {
        await using var service = CreateService();
        var exception = new InvalidOperationException(@"Private title at C:\Users\private-user\Music\secret.mp3");

        service.Write(
            TechnicalLogLevel.Error,
            "TEST_FAILURE",
            "Tests",
            @"Failed while reading D:\Private\track-name.mp3",
            exception,
            new Dictionary<string, object?> { ["title"] = "Private title", ["operation"] = "read" });
        await service.FlushAsync();

        var content = await File.ReadAllTextAsync(Directory.GetFiles(_directory, "*.ndjson").Single());
        Assert.DoesNotContain("Private title", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-user", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret.mp3", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("track-name.mp3", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(exception.Message, content, StringComparison.Ordinal);
        StringAssert.Contains(content, "System.InvalidOperationException");
        StringAssert.Contains(content, "operation");
    }

    [TestMethod]
    public async Task BurstWrites_RemainNonBlockingAndFlushCompletes()
    {
        await using var service = CreateService();
        var stopwatch = Stopwatch.StartNew();

        Parallel.For(0, 10_000, index => service.Write(
            TechnicalLogLevel.Trace,
            "BURST",
            "Stress",
            "Synthetic bounded queue event.",
            properties: new Dictionary<string, object?> { ["count"] = index }));
        stopwatch.Stop();
        await service.FlushAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        var entries = await service.ReadLatestAsync(1000);
        Assert.IsNotEmpty(entries);
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Producers blocked for {stopwatch.Elapsed}.");
        Assert.IsTrue(service.DroppedEntryCount >= 0);
    }

    [TestMethod]
    public async Task RotationClearAndExport_ManageFilesSafely()
    {
        var retention = new LogRetentionPolicy(64 * 1024, 10, TimeSpan.FromDays(14));
        await using var service = CreateService(retention);
        var message = new string('x', 1000);
        for (var index = 0; index < 180; index++)
        {
            service.Write(TechnicalLogLevel.Information, "ROTATION", "Tests", message);
        }

        await service.FlushAsync();
        Assert.IsGreaterThan(1, Directory.GetFiles(_directory, "*.ndjson").Length);

        var archivePath = Path.Combine(Path.GetTempPath(), "MiaDockTests", $"{Guid.NewGuid():N}.zip");
        try
        {
            await service.ExportAsync(archivePath);
            using var archive = ZipFile.OpenRead(archivePath);
            Assert.IsNotNull(archive.GetEntry("export-manifest.json"));
            Assert.IsNotNull(archive.GetEntry("diagnostic-timeline.json"));
            Assert.IsNotNull(archive.GetEntry("event-summary.json"));
            Assert.IsNotNull(archive.GetEntry("BUG-REPORT-README.txt"));
            Assert.IsTrue(archive.Entries.Any(entry => entry.Name.EndsWith(".ndjson", StringComparison.Ordinal)));

            using var manifestReader = new StreamReader(archive.GetEntry("export-manifest.json")!.Open());
            var manifest = await manifestReader.ReadToEndAsync();
            StringAssert.Contains(manifest, "MiaDock technical logs v2");
            StringAssert.Contains(manifest, "DroppedEntryCount");
        }
        finally
        {
            File.Delete(archivePath);
        }

        await service.ClearAsync();
        Assert.HasCount(0, Directory.GetFiles(_directory, "*.ndjson"));
    }

    [TestMethod]
    public async Task ReadLatest_IgnoresCorruptLines()
    {
        await using var service = CreateService();
        service.Write(TechnicalLogLevel.Information, "VALID", "Tests", "Valid event.");
        await service.FlushAsync();
        var path = Directory.GetFiles(_directory, "*.ndjson").Single();
        await File.AppendAllTextAsync(path, "{not-json}" + Environment.NewLine);

        var entries = await service.ReadLatestAsync();

        Assert.HasCount(1, entries);
        Assert.AreEqual("VALID", entries[0].EventId);
    }

    [TestMethod]
    public async Task SensitiveModuleProperties_AreNeverPersisted()
    {
        await using var service = CreateService();
        service.Write(
            TechnicalLogLevel.Information,
            "PRIVACY_AUDIT",
            "Modules",
            "Module state changed.",
            properties: new Dictionary<string, object?>
            {
                ["moduleId"] = "notifications",
                ["notificationBody"] = "Confidential notification body",
                ["bluetoothDeviceName"] = "Private headphones",
                ["meetingName"] = "Secret project meeting",
                ["filePath"] = @"C:\Users\private-user\Documents\private-transfer.zip",
                ["transferName"] = "private-transfer.zip"
            });
        await service.FlushAsync();

        var content = await File.ReadAllTextAsync(Directory.GetFiles(_directory, "*.ndjson").Single());

        StringAssert.Contains(content, "notifications");
        Assert.DoesNotContain("Confidential notification body", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Private headphones", content, StringComparison.Ordinal);
        Assert.DoesNotContain("Secret project meeting", content, StringComparison.Ordinal);
        Assert.DoesNotContain("private-transfer", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-user", content, StringComparison.OrdinalIgnoreCase);
    }

    private JsonLinesLogService CreateService(LogRetentionPolicy? retention = null) =>
        new(new TestLogPathProvider(_directory), new SensitiveDataRedactor(), retention);

    private sealed class TestLogPathProvider(string path) : ILogPathProvider
    {
        public string GetLogDirectoryPath() => path;
    }
}
