using MiaDock.Core.Clipboard;
using MiaDock.Core.Logging;
using MiaDock.Core.Threading;
using MiaDock.Platform.Windows.Clipboard;
using MiaDock.Platform.Windows.Lifecycle;

namespace MiaDock.Platform.Windows.Tests.Clipboard;

[TestClass]
public sealed class WindowsClipboardPeekServiceTests
{
    [TestMethod]
    public async Task InitialSnapshot_IsVisibleWithoutCapturedEvent_AndStopClearsRamState()
    {
        var fixture = CreateFixture(new ClipboardPlatformSnapshot(ClipboardPlatformContentKind.Text, Text: "hello"));
        var captured = 0;
        fixture.Service.ItemCaptured += (_, _) => captured++;

        await fixture.Service.StartAsync();

        Assert.AreEqual("hello", fixture.Service.Current.CurrentItem?.RawText);
        Assert.IsTrue(fixture.Service.Current.IsInitialSnapshot);
        Assert.AreEqual(0, captured);
        await fixture.Service.StopAsync();
        Assert.IsNull(fixture.Service.Current.CurrentItem);
        Assert.AreEqual(0, fixture.Service.Current.History.Count);
        Assert.AreEqual(1, fixture.Platform.StartCount);
        Assert.AreEqual(1, fixture.Platform.StopCount);
    }

    [TestMethod]
    public async Task RapidChanges_CoalesceAndNewestGenerationWins()
    {
        var fixture = CreateFixture(new ClipboardPlatformSnapshot(ClipboardPlatformContentKind.Text, Text: "initial"));
        await fixture.Service.StartAsync();
        var gate = fixture.Platform.BlockNextRead();
        fixture.Platform.Enqueue(new ClipboardPlatformSnapshot(ClipboardPlatformContentKind.Text, Text: "old"));
        fixture.Platform.RaiseChanged();
        await fixture.Platform.WaitUntilBlockedAsync();
        fixture.Platform.Enqueue(new ClipboardPlatformSnapshot(ClipboardPlatformContentKind.Text, Text: "new"));
        fixture.Platform.RaiseChanged();

        gate.SetResult();
        await WaitUntilAsync(() => fixture.Service.Current.CurrentItem?.RawText == "new");

        Assert.AreEqual("new", fixture.Service.Current.CurrentItem?.RawText);
        Assert.IsFalse(fixture.Service.Current.History.Any(item => item.RawText == "old"));
    }

    [TestMethod]
    public async Task DuplicateAndSelfWrite_DoNotRaiseCapturedEvent()
    {
        var fixture = CreateFixture(new ClipboardPlatformSnapshot(ClipboardPlatformContentKind.Text, Text: "same"));
        await fixture.Service.StartAsync();
        var captured = 0;
        fixture.Service.ItemCaptured += (_, _) => captured++;

        fixture.Platform.Enqueue(new ClipboardPlatformSnapshot(ClipboardPlatformContentKind.Text, Text: "same"));
        fixture.Platform.RaiseChanged();
        await Task.Delay(50);
        Assert.AreEqual(0, captured);

        var result = await fixture.Service.CopyAsync(fixture.Service.Current.CurrentItem!);
        await Task.Delay(50);
        Assert.AreEqual(ClipboardPeekActionResult.Succeeded, result);
        Assert.AreEqual(0, captured);
        Assert.AreEqual("same", fixture.Platform.LastWrittenText);
    }

    [TestMethod]
    public async Task CopyTextAsync_SelfWrite_DoesNotRaiseCapturedEvent()
    {
        var fixture = CreateFixture(new ClipboardPlatformSnapshot(ClipboardPlatformContentKind.Text, Text: "hello"));
        await fixture.Service.StartAsync();
        var captured = 0;
        fixture.Service.ItemCaptured += (_, _) => captured++;

        var result = await fixture.Service.CopyTextAsync("rgb(74, 144, 226)");
        await Task.Delay(50);

        Assert.AreEqual(ClipboardPeekActionResult.Succeeded, result);
        Assert.AreEqual(0, captured);
        Assert.AreEqual("rgb(74, 144, 226)", fixture.Platform.LastWrittenText);
        Assert.AreEqual("hello", fixture.Service.Current.CurrentItem?.RawText);
        Assert.AreEqual(ClipboardPeekActionResult.Unavailable, await fixture.Service.CopyTextAsync("  "));
    }

    [TestMethod]
    public async Task SensitiveValue_NeverEntersState_AndCanBeRevealedOnlyOnce()
    {
        var fixture = CreateFixture(new ClipboardPlatformSnapshot(ClipboardPlatformContentKind.Text, Text: "123456"));
        await fixture.Service.StartAsync();
        var item = fixture.Service.Current.CurrentItem!;

        Assert.AreEqual(ClipboardPeekContentType.Sensitive, item.Type);
        Assert.IsNull(item.RawText);
        Assert.AreEqual(0, fixture.Service.Current.History.Count);
        var first = await fixture.Service.RevealSensitiveAsync(item.Id);
        var second = await fixture.Service.RevealSensitiveAsync(item.Id);

        Assert.AreEqual(ClipboardPeekActionResult.Succeeded, first.Result);
        Assert.AreEqual("123456", first.Value);
        Assert.AreEqual(ClipboardPeekActionResult.Unavailable, second.Result);
        Assert.IsNull(second.Value);
    }

    [TestMethod]
    public async Task SensitiveValue_IsClearedOnLockAndExpiry()
    {
        var fixture = CreateFixture(new ClipboardPlatformSnapshot(ClipboardPlatformContentKind.Text, Text: "123456"));
        await fixture.Service.StartAsync();
        var firstId = fixture.Service.Current.CurrentItem!.Id;
        fixture.SessionLock.Raise(true);
        Assert.AreEqual(ClipboardPeekActionResult.Unavailable,
            (await fixture.Service.RevealSensitiveAsync(firstId)).Result);

        fixture.SessionLock.Raise(false);
        fixture.Platform.Enqueue(new ClipboardPlatformSnapshot(ClipboardPlatformContentKind.Text, Text: "654321"));
        fixture.Platform.RaiseChanged();
        await WaitUntilAsync(() => fixture.Service.Current.CurrentItem?.Id != firstId);
        var secondId = fixture.Service.Current.CurrentItem!.Id;
        fixture.Time.Advance(TimeSpan.FromMinutes(6));
        Assert.AreEqual(ClipboardPeekActionResult.Unavailable,
            (await fixture.Service.RevealSensitiveAsync(secondId)).Result);
    }

    [TestMethod]
    public async Task ImageSave_UsesCapturedPngAfterClipboardChanges()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3, 4];
        var image = new ClipboardPlatformImage(100, 80, png, png);
        var fixture = CreateFixture(new ClipboardPlatformSnapshot(ClipboardPlatformContentKind.Image, Image: image));
        await fixture.Service.StartAsync();
        var imageItem = fixture.Service.Current.CurrentItem!;
        fixture.Platform.Enqueue(new ClipboardPlatformSnapshot(ClipboardPlatformContentKind.Text, Text: "later"));
        fixture.Platform.RaiseChanged();
        await WaitUntilAsync(() => fixture.Service.Current.CurrentItem?.RawText == "later");

        var result = await fixture.Service.SaveImageAsync(imageItem, (nint)123);

        Assert.AreEqual(ClipboardPeekActionResult.Succeeded, result);
        CollectionAssert.AreEqual(png, fixture.Platform.LastSavedPng);
    }

    [TestMethod]
    public async Task MultipleStorageItems_ExposeOnlySafeCountSummary()
    {
        var items = new[]
        {
            new ClipboardPlatformStorageItem("a.txt", "C:\\private\\a.txt", false),
            new ClipboardPlatformStorageItem("b.txt", "C:\\private\\b.txt", false)
        };
        var fixture = CreateFixture(new ClipboardPlatformSnapshot(
            ClipboardPlatformContentKind.StorageItems, StorageItems: items));

        await fixture.Service.StartAsync();

        var item = fixture.Service.Current.CurrentItem!;
        Assert.AreEqual(2, item.ItemCount);
        Assert.IsNull(item.RawText);
        Assert.IsNull(item.FilePath);
        Assert.AreEqual(ClipboardPeekCapabilities.None, item.AvailableActions);
    }

    [TestMethod]
    [DataRow(-5, 0)]
    [DataRow(3, 5)]
    [DataRow(7, 5)]
    [DataRow(8, 10)]
    [DataRow(15, 20)]
    [DataRow(99, 20)]
    public void HistoryLimit_NormalizesToSupportedValuesWithTiesUp(int value, int expected) =>
        Assert.AreEqual(expected, WindowsClipboardPeekService.NormalizeHistoryLimit(value));

    private static Fixture CreateFixture(ClipboardPlatformSnapshot initial)
    {
        var platform = new FakeClipboardPlatformAdapter(initial);
        var settings = new FakeSettings();
        var session = new FakeSessionLock();
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero));
        var service = new WindowsClipboardPeekService(
            new ImmediateDispatcher(), settings, new NullLogService(), session, time, platform);
        return new Fixture(service, platform, session, time);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var timeout = DateTime.UtcNow.AddSeconds(3);
        while (!predicate() && DateTime.UtcNow < timeout) await Task.Delay(10);
        Assert.IsTrue(predicate(), "Timed out waiting for clipboard service state.");
    }

    private sealed record Fixture(
        WindowsClipboardPeekService Service,
        FakeClipboardPlatformAdapter Platform,
        FakeSessionLock SessionLock,
        ManualTimeProvider Time);

    private sealed class FakeClipboardPlatformAdapter(params ClipboardPlatformSnapshot[] initial) : IClipboardPlatformAdapter
    {
        private readonly Queue<ClipboardPlatformSnapshot> _snapshots = new(initial);
        private TaskCompletionSource? _readGate;
        private TaskCompletionSource? _blocked;
        public event EventHandler? ContentChanged;
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public string? LastWrittenText { get; private set; }
        public byte[]? LastSavedPng { get; private set; }
        public void Start() => StartCount++;
        public void Stop() => StopCount++;
        public void Enqueue(ClipboardPlatformSnapshot snapshot) => _snapshots.Enqueue(snapshot);
        public void RaiseChanged() => ContentChanged?.Invoke(this, EventArgs.Empty);
        public TaskCompletionSource BlockNextRead()
        {
            _readGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return _readGate;
        }
        public Task WaitUntilBlockedAsync() => _blocked?.Task ?? Task.CompletedTask;
        public async Task<ClipboardPlatformSnapshot?> ReadAsync(CancellationToken cancellationToken)
        {
            var snapshot = _snapshots.Count == 0 ? null : _snapshots.Dequeue();
            if (_readGate is { } gate)
            {
                _readGate = null;
                _blocked?.TrySetResult();
                await gate.Task.WaitAsync(cancellationToken);
            }
            return snapshot;
        }
        public Task WriteTextAsync(string text, CancellationToken cancellationToken)
        {
            LastWrittenText = text;
            Enqueue(new ClipboardPlatformSnapshot(ClipboardPlatformContentKind.Text, Text: text));
            RaiseChanged();
            return Task.CompletedTask;
        }
        public Task<ClipboardPeekActionResult> SavePngAsync(byte[] png, nint ownerWindow, CancellationToken cancellationToken)
        {
            LastSavedPng = png.ToArray();
            return Task.FromResult(ClipboardPeekActionResult.Succeeded);
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeSettings : IClipboardPeekSettings
    {
        public ClipboardPeekOptions Current { get; private set; } = ClipboardPeekOptions.Default;
        public TimeSpan EventDuration => TimeSpan.FromSeconds(3);
        public event EventHandler<ClipboardPeekOptions>? Changed;
        public void Set(ClipboardPeekOptions options)
        {
            Current = options;
            Changed?.Invoke(this, options);
        }
    }

    private sealed class FakeSessionLock : IWindowsSessionLockStateService
    {
        public bool IsLocked { get; private set; }
        public event EventHandler<bool>? LockStateChanged;
        public void Start() { }
        public void Raise(bool value)
        {
            IsLocked = value;
            LockStateChanged?.Invoke(this, value);
        }
        public void Dispose() { }
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public bool HasThreadAccess => true;
        public bool TryEnqueue(Action callback) { callback(); return true; }
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class NullLogService : ILogService
    {
        public string LogDirectoryPath => string.Empty;
        public Exception? LastFailure => null;
        public long DroppedEntryCount => 0;
        public void Write(TechnicalLogLevel level, string eventId, string category, string message,
            Exception? exception = null, IReadOnlyDictionary<string, object?>? properties = null) { }
        public ValueTask FlushAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
