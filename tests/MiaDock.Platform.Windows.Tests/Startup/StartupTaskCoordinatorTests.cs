using MiaDock.App.Services;
using MiaDock.Core.Logging;
using MiaDock.Core.Settings;
using MiaDock.Platform.Windows.Startup;

namespace MiaDock.Platform.Windows.Tests.Startup;

[TestClass]
public sealed class StartupTaskCoordinatorTests
{
    [TestMethod]
    public async Task DesiredStartup_RepairsDisabledWindowsTask()
    {
        var startup = new FakeStartupTaskService(StartupTaskStatus.Disabled);
        var settings = new FakeSettingsService
        {
            Current = WithStartupPreference(enabled: true)
        };
        var coordinator = new StartupTaskCoordinator(
            startup,
            settings,
            new NullLogService());

        var result = await coordinator.ReconcileAsync();

        Assert.AreEqual(StartupTaskStatus.Enabled, result);
        Assert.AreEqual(1, startup.SetEnabledCount);
        Assert.IsTrue(startup.LastRequestedEnabled);
        Assert.IsTrue(settings.Current.StartupShutdown.StartWithWindows);
    }

    [TestMethod]
    public async Task DisabledPreference_DisablesEnabledWindowsTask()
    {
        var startup = new FakeStartupTaskService(StartupTaskStatus.Enabled);
        var settings = new FakeSettingsService
        {
            Current = WithStartupPreference(enabled: false)
        };
        var coordinator = new StartupTaskCoordinator(
            startup,
            settings,
            new NullLogService());

        await coordinator.ReconcileAsync();

        Assert.IsFalse(settings.Current.StartupShutdown.StartWithWindows);
        Assert.AreEqual(1, startup.SetEnabledCount);
        Assert.IsFalse(startup.LastRequestedEnabled);
    }

    [TestMethod]
    public async Task DisabledByUser_IsNotProgrammaticallyOverridden()
    {
        var startup = new FakeStartupTaskService(StartupTaskStatus.DisabledByUser);
        var settings = new FakeSettingsService
        {
            Current = WithStartupPreference(enabled: true)
        };
        var coordinator = new StartupTaskCoordinator(
            startup,
            settings,
            new NullLogService());

        var result = await coordinator.ReconcileAsync();

        Assert.AreEqual(StartupTaskStatus.DisabledByUser, result);
        Assert.AreEqual(0, startup.SetEnabledCount);
        Assert.IsFalse(settings.Current.StartupShutdown.StartWithWindows);
    }

    private static MiaDockSettings WithStartupPreference(bool enabled) =>
        MiaDockSettings.Default with
        {
            StartupShutdown = MiaDockSettings.Default.StartupShutdown with
            {
                StartWithWindows = enabled
            }
        };

    private sealed class FakeStartupTaskService(StartupTaskStatus status) : IStartupTaskService
    {
        public StartupTaskStatus Status { get; private set; } = status;
        public int SetEnabledCount { get; private set; }
        public bool LastRequestedEnabled { get; private set; }

        public Task<StartupTaskStatus> GetStatusAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Status);

        public Task<StartupTaskStatus> SetEnabledAsync(
            bool enabled,
            CancellationToken cancellationToken = default)
        {
            SetEnabledCount++;
            LastRequestedEnabled = enabled;
            Status = enabled
                ? StartupTaskStatus.Enabled
                : StartupTaskStatus.Disabled;
            return Task.FromResult(Status);
        }
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
            SettingsChanged?.Invoke(
                this,
                new SettingsChangedEventArgs(previous, Current));
        }

        public void Reset() => Current = MiaDockSettings.Default;

        public Task FlushAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
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
