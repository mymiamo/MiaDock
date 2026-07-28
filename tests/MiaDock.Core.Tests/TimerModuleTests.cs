using MiaDock.Modules.Time;
using MiaDock.Core.Modules;
using MiaDock.Modules.Time.Models;
using MiaDock.Modules.Time.Services;
using MiaDock.Modules.Time.ViewModels;
using MiaDock.Core.Settings;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class TimerModuleTests
{
    [TestMethod]
    [DataRow(true, 0)]
    [DataRow(false, 5_000)]
    public async Task RunningOrPausedStopwatch_KeepsModulePersistent(
        bool isRunning,
        int elapsedMilliseconds)
    {
        var service = new FakeTimeToolsService(TimeToolsSnapshot.Default with
        {
            IsStopwatchRunning = isRunning,
            StopwatchElapsed = TimeSpan.FromMilliseconds(elapsedMilliseconds)
        });
        using var viewModel = new TimeToolsViewModel(service);
        using var module = new TimerModule(viewModel, service);

        await module.ActivateAsync();

        Assert.IsTrue(module.CurrentPresentation?.IsPersistentOverride);
        Assert.AreEqual(500, module.CurrentPresentation?.PersistentPriorityOverride);
    }

    [TestMethod]
    public async Task ResetStopwatch_ReturnsModuleToPassivePresentation()
    {
        var service = new FakeTimeToolsService(TimeToolsSnapshot.Default with
        {
            StopwatchElapsed = TimeSpan.FromSeconds(5)
        });
        using var viewModel = new TimeToolsViewModel(service);
        using var module = new TimerModule(viewModel, service);
        await module.ActivateAsync();

        service.Publish(TimeToolsSnapshot.Default);

        Assert.IsFalse(module.CurrentPresentation?.IsPersistentOverride);
        Assert.IsNull(module.CurrentPresentation?.PersistentPriorityOverride);
    }

    [TestMethod]
    public async Task RunningStopwatch_PresentsElapsedTimeInsteadOfIdleTimer()
    {
        var service = new FakeTimeToolsService(TimeToolsSnapshot.Default with
        {
            IsStopwatchRunning = true,
            StopwatchElapsed = TimeSpan.FromSeconds(65)
        });
        using var viewModel = new TimeToolsViewModel(service);
        using var module = new TimerModule(viewModel, service);

        await module.ActivateAsync();

        Assert.AreEqual("00:01:05", module.CurrentPresentation?.PrimaryText);
        Assert.AreEqual("Kronometre çalışıyor", module.CurrentPresentation?.SecondaryText);
        Assert.IsTrue(module.CanExecuteCommand("pause-resume"));
        Assert.IsFalse(module.CanExecuteCommand("cancel"));
    }

    [TestMethod]
    public async Task RunningStopwatch_LanguageChangeRefreshesActivePresentation()
    {
        var localization = new TestLocalizationService(
            new Dictionary<string, (string Turkish, string English)>
            {
                ["Timer.StopwatchRunning"] = ("Kronometre çalışıyor", "Stopwatch running")
            });
        var service = new FakeTimeToolsService(TimeToolsSnapshot.Default with
        {
            IsStopwatchRunning = true,
            StopwatchElapsed = TimeSpan.FromSeconds(5)
        });
        using var viewModel = new TimeToolsViewModel(service, localization: localization);
        using var module = new TimerModule(viewModel, service, localization);
        await module.ActivateAsync();
        ModulePresentation? updated = null;
        module.PresentationChanged += (_, presentation) => updated = presentation;

        localization.SetLanguage(AppLanguage.English);

        Assert.AreEqual("Stopwatch running", viewModel.CompactStatusText);
        Assert.AreEqual("Stopwatch running", updated?.SecondaryText);
    }

    [TestMethod]
    public async Task CompletedTimer_RemainsPersistentUntilAlarmIsDismissed()
    {
        var service = new FakeTimeToolsService(TimeToolsSnapshot.Default with
        {
            TimerState = TimerRunState.Completed,
            TimerDuration = TimeSpan.FromMinutes(5)
        });
        using var viewModel = new TimeToolsViewModel(service);
        using var module = new TimerModule(viewModel, service);

        await module.ActivateAsync();

        Assert.IsTrue(module.CurrentPresentation?.IsPersistentOverride);
        Assert.AreEqual("Süre doldu", module.CurrentPresentation?.SecondaryText);
        Assert.AreEqual("Alarmı sustur", module.CurrentPresentation?.Commands.Single(command => command.Id == "cancel").DisplayName);
        Assert.IsTrue(module.CanExecuteCommand("cancel"));
    }

    private sealed class FakeTimeToolsService(TimeToolsSnapshot current) : ITimeToolsService
    {
        public TimeToolsSnapshot Current { get; private set; } = current;

        public event EventHandler<TimeToolsSnapshot>? SnapshotChanged;

        public Task InitializeAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public bool StartTimer(TimeSpan duration) => false;
        public bool PauseTimer() => false;
        public bool ResumeTimer() => false;
        public bool CancelTimer() => false;
        public bool ConsumePendingCompletion() => false;
        public bool StartStopwatch() => false;
        public bool PauseStopwatch() => false;
        public bool AddLap() => false;
        public bool ResetStopwatch() => false;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void Publish(TimeToolsSnapshot snapshot)
        {
            Current = snapshot;
            SnapshotChanged?.Invoke(this, snapshot);
        }
    }
}
