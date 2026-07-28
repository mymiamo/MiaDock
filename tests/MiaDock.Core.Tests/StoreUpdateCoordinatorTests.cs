using MiaDock.App.Modules;
using MiaDock.App.Services;
using MiaDock.Core.Logging;
using MiaDock.Core.Modules;
using MiaDock.Core.Settings;
using MiaDock.Core.Threading;
using MiaDock.Core.Updates;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class StoreUpdateCoordinatorTests
{
    [TestMethod]
    public async Task SameVersion_NotifiesOnce_NewerVersionNotifiesAgain()
    {
        var settings = new FakeSettingsService();
        var store = new FakeStoreUpdateService
        {
            Result = Available("1.2.0.0")
        };
        var module = new StoreUpdateModule(store, Localizer());
        await module.ActivateAsync();
        var notifications = new List<ModuleEvent>();
        module.EventOccurred += (_, value) => notifications.Add(value);
        await using var coordinator = new StoreUpdateCoordinator(
            store,
            settings,
            module,
            new ImmediateDispatcher(),
            new NullLogService());

        await coordinator.CheckNowAsync();
        settings.MoveLastCheckToPast();
        await coordinator.CheckNowAsync();
        store.Result = Available("1.3.0.0");
        settings.MoveLastCheckToPast();
        await coordinator.CheckNowAsync();

        Assert.HasCount(2, notifications);
        Assert.AreEqual(
            "store-update:1.2.0.0",
            notifications[0].CoalescingKey);
        Assert.AreEqual(
            "store-update:1.3.0.0",
            notifications[1].CoalescingKey);
        Assert.AreEqual(
            "1.3.0.0",
            settings.Current.StoreUpdates.LastNotifiedVersion);
        Assert.AreEqual(3, store.CheckCount);
    }

    [TestMethod]
    public async Task RecentPersistedCheck_ThrottlesManualStoreQuery()
    {
        var settings = new FakeSettingsService
        {
            Current = MiaDockSettings.Default with
            {
                StoreUpdates = StoreUpdateSettings.Default with
                {
                    LastCheckUtc = DateTimeOffset.UtcNow
                }
            }
        };
        var store = new FakeStoreUpdateService
        {
            Result = Available("1.2.0.0")
        };
        var module = new StoreUpdateModule(store, Localizer());
        await module.ActivateAsync();
        await using var coordinator = new StoreUpdateCoordinator(
            store,
            settings,
            module,
            new ImmediateDispatcher(),
            new NullLogService());

        await coordinator.CheckNowAsync();

        Assert.AreEqual(0, store.CheckCount);
    }

    [TestMethod]
    public async Task AutomaticPreference_IsPersisted()
    {
        var settings = new FakeSettingsService();
        var store = new FakeStoreUpdateService();
        var module = new StoreUpdateModule(store, Localizer());
        await using var coordinator = new StoreUpdateCoordinator(
            store,
            settings,
            module,
            new ImmediateDispatcher(),
            new NullLogService());

        coordinator.SetAutomaticChecksEnabled(false);

        Assert.IsFalse(settings.Current.StoreUpdates.AutomaticChecksEnabled);
    }

    private static StoreUpdateSnapshot Available(string version) =>
        new(
            StoreUpdateStatus.UpdateAvailable,
            new Version(1, 1, 0, 0),
            Version.Parse(version),
            DateTimeOffset.UtcNow);

    private static TestLocalizationService Localizer() =>
        new(new Dictionary<string, (string Turkish, string English)>
        {
            ["Update.Available"] = ("Yeni sürüm mevcut", "A new version is available"),
            ["Update.VersionPair"] = ("MiaDock {0} → {1}", "MiaDock {0} → {1}"),
            ["Update.OpenStore"] = ("Microsoft Store'da aç", "Open in Microsoft Store")
        });

    private sealed class FakeStoreUpdateService : IStoreUpdateService
    {
        public StoreUpdateSnapshot Result { get; set; } =
            StoreUpdateSnapshot.Unavailable(new Version(1, 1, 0, 0));
        public StoreUpdateSnapshot Current { get; private set; } =
            StoreUpdateSnapshot.Unavailable(new Version(1, 1, 0, 0));
        public int CheckCount { get; private set; }
        public event EventHandler<StoreUpdateSnapshot>? UpdateAvailabilityChanged;

        public Task<StoreUpdateSnapshot> CheckAsync(
            CancellationToken cancellationToken = default)
        {
            CheckCount++;
            Current = Result;
            UpdateAvailabilityChanged?.Invoke(this, Result);
            return Task.FromResult(Result);
        }

        public Task<bool> OpenStorePageAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public MiaDockSettings Current { get; set; } = MiaDockSettings.Default;
        public Exception? LastSaveFailure => null;
        public string SettingsFilePath => string.Empty;
        public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

        public Task InitializeAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void Update(Func<MiaDockSettings, MiaDockSettings> update)
        {
            var previous = Current;
            Current = SettingsValidator.Normalize(update(Current));
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs(previous, Current));
        }

        public void Reset() => Current = MiaDockSettings.Default;

        public Task FlushAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void MoveLastCheckToPast() =>
            Current = Current with
            {
                StoreUpdates = Current.StoreUpdates with
                {
                    LastCheckUtc = DateTimeOffset.UtcNow.AddMinutes(-4)
                }
            };
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public bool HasThreadAccess => true;
        public bool TryEnqueue(Action callback)
        {
            callback();
            return true;
        }
    }

    private sealed class NullLogService : ILogService
    {
        public string LogDirectoryPath => string.Empty;
        public Exception? LastFailure => null;
        public long DroppedEntryCount => 0;
        public void Write(
            TechnicalLogLevel level,
            string eventId,
            string category,
            string message,
            Exception? exception = null,
            IReadOnlyDictionary<string, object?>? properties = null)
        {
        }
        public ValueTask FlushAsync(CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
