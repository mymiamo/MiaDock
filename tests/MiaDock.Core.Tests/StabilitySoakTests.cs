using MiaDock.Core.Modules;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class StabilitySoakTests
{
    [TestMethod]
    [TestCategory("Soak")]
    public async Task IntensiveEvents_ThirtyMinutes_RemainBounded()
    {
        if (!ShouldRun("events")) return;

        var duration = Scale(TimeSpan.FromMinutes(30));
        var module = new SoakModule();
        await using var registry = new IslandModuleRegistry([module]);
        using var orchestrator = new ModuleOrchestrator(registry);
        await registry.InitializeAsync();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        var initialMemory = GC.GetTotalMemory(forceFullCollection: true);
        var deadline = DateTimeOffset.UtcNow + duration;
        var index = 0;

        while (DateTimeOffset.UtcNow < deadline)
        {
            for (var burst = 0; burst < 128; burst++)
            {
                module.Publish(index++, $"soak-{index % 32}");
            }
            orchestrator.CompleteActiveEvent();
            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var finalMemory = GC.GetTotalMemory(forceFullCollection: true);
        Assert.IsLessThanOrEqualTo(ModuleOrchestrator.MaximumPendingEvents, orchestrator.PendingEventCount);
        Assert.IsTrue(finalMemory - initialMemory < 64 * 1024 * 1024,
            $"Managed memory grew by {finalMemory - initialMemory:N0} bytes.");
    }

    [TestMethod]
    [TestCategory("Soak")]
    public async Task Idle_EightHours_DoesNotCreateEventsOrPendingWork()
    {
        if (!ShouldRun("idle")) return;

        var duration = Scale(TimeSpan.FromHours(8));
        var module = new SoakModule();
        await using var registry = new IslandModuleRegistry([module]);
        using var orchestrator = new ModuleOrchestrator(registry);
        await registry.InitializeAsync();
        var deadline = DateTimeOffset.UtcNow + duration;

        while (DateTimeOffset.UtcNow < deadline)
        {
            _ = orchestrator.CurrentDisplay;
            _ = orchestrator.PendingEventCount;
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        Assert.AreEqual(0, orchestrator.Statistics.ReceivedEvents);
        Assert.AreEqual(0, orchestrator.PendingEventCount);
    }

    private static bool ShouldRun(string profile)
    {
        var selected = Environment.GetEnvironmentVariable("MIADOCK_SOAK_PROFILE");
        return string.Equals(selected, "all", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(selected, profile, StringComparison.OrdinalIgnoreCase);
    }

    private static TimeSpan Scale(TimeSpan duration)
    {
        var value = Environment.GetEnvironmentVariable("MIADOCK_SOAK_SCALE");
        return double.TryParse(
                   value,
                   System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture,
                   out var scale)
               && scale is > 0 and <= 1
            ? duration * scale
            : duration;
    }

    private sealed class SoakModule : IIslandModule
    {
        public SoakModule()
        {
            Descriptor = new ModuleDescriptor(
                "soak",
                "Soak",
                1,
                "soak.compact",
                "soak.expanded",
                new HashSet<ModuleEventKind> { ModuleEventKind.StatusChanged },
                TimeSpan.FromSeconds(5));
            CurrentPresentation = Presentation("idle");
        }

        public ModuleDescriptor Descriptor { get; }
        public ModuleLifecycleState LifecycleState { get; private set; }
        public bool IsEnabled { get; set; } = true;
        public ModulePresentation? CurrentPresentation { get; private set; }
        public event EventHandler<ModulePresentation?>? PresentationChanged;
        public event EventHandler<ModuleEvent>? EventOccurred;

        public bool CanExecuteCommand(string commandId) => false;
        public ValueTask<bool> ExecuteCommandAsync(string commandId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);

        public ValueTask ActivateAsync(CancellationToken cancellationToken = default)
        {
            LifecycleState = ModuleLifecycleState.Active;
            return ValueTask.CompletedTask;
        }

        public ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
        {
            LifecycleState = ModuleLifecycleState.Inactive;
            return ValueTask.CompletedTask;
        }

        public void Publish(int index, string key)
        {
            CurrentPresentation = Presentation(index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            PresentationChanged?.Invoke(this, CurrentPresentation);
            EventOccurred?.Invoke(this, new ModuleEvent(
                Descriptor.Id,
                ModuleEventKind.StatusChanged,
                CurrentPresentation,
                TimeSpan.FromSeconds(5),
                DateTimeOffset.UtcNow,
                index % 97 == 0 ? ModuleEventPriority.Critical : ModuleEventPriority.Normal,
                key));
        }

        private static ModulePresentation Presentation(string text) => new(
            "soak",
            text,
            string.Empty,
            "\uE9D9",
            ModuleIndicatorKind.None);
    }
}
