using MiaDock.Modules.Time;
using MiaDock.Modules.Time.Models;
using MiaDock.Modules.Time.Services;
using MiaDock.Modules.Time.ViewModels;

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
