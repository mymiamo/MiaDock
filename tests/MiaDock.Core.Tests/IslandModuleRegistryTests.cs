using MiaDock.Core.Modules;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class IslandModuleRegistryTests
{
    [TestMethod]
    public async Task Initialize_SelectsHighestPriorityActivePresentation()
    {
        var low = new TestModule("low", 10, new ModulePresentation("low", "Low", "", "", ModuleIndicatorKind.None));
        var high = new TestModule("high", 100, new ModulePresentation("high", "High", "", "", ModuleIndicatorKind.StatusDot));
        await using var registry = new IslandModuleRegistry([low, high]);

        await registry.InitializeAsync();

        Assert.AreSame(high, registry.ActiveModule);
        Assert.AreEqual("high", registry.ActivePresentation?.ModuleId);
    }

    [TestMethod]
    public async Task ModuleEvent_IsForwardedWithoutKnowingModuleType()
    {
        var module = new TestModule("timer", 20, new ModulePresentation("timer", "01:00", "", "T", ModuleIndicatorKind.Value));
        await using var registry = new IslandModuleRegistry([module]);
        ModuleEvent? received = null;
        registry.ModuleEventOccurred += (_, value) => received = value;
        await registry.InitializeAsync();

        module.RaiseEvent(ModuleEventKind.TimelineChanged);

        Assert.IsNotNull(received);
        Assert.AreEqual("timer", received.ModuleId);
        Assert.AreEqual(ModuleEventKind.TimelineChanged, received.Kind);
    }

    [TestMethod]
    public void Constructor_RejectsDuplicateModuleIds()
    {
        var first = new TestModule("same", 1, null);
        var second = new TestModule("same", 2, null);

        Assert.ThrowsExactly<InvalidOperationException>(() => new IslandModuleRegistry([first, second]));
    }

    [TestMethod]
    public async Task ExecuteCommandAsync_RoutesToActiveModule()
    {
        var module = new TestModule(
            "module",
            1,
            new ModulePresentation("module", "Active", "", "", ModuleIndicatorKind.None));
        await using var registry = new IslandModuleRegistry([module]);
        await registry.InitializeAsync();

        Assert.IsTrue(registry.CanExecuteCommand("module", "test"));
        Assert.IsTrue(await registry.ExecuteCommandAsync("module", "test"));
        Assert.IsFalse(await registry.ExecuteCommandAsync("module", "unknown"));
    }

    [TestMethod]
    public async Task SetEnabledAsync_ActivatesAndDeactivatesModuleAtRuntime()
    {
        var module = new TestModule(
            "notifications",
            1,
            new ModulePresentation("notifications", "Bildirim", "", "", ModuleIndicatorKind.None))
        {
            IsEnabled = false
        };
        await using var registry = new IslandModuleRegistry([module]);
        await registry.InitializeAsync();

        Assert.AreEqual(ModuleLifecycleState.Inactive, module.LifecycleState);
        Assert.IsTrue(await registry.SetEnabledAsync("notifications", true));
        Assert.AreEqual(ModuleLifecycleState.Active, module.LifecycleState);
        Assert.IsTrue(module.IsEnabled);

        Assert.IsTrue(await registry.SetEnabledAsync("notifications", false));
        Assert.AreEqual(ModuleLifecycleState.Inactive, module.LifecycleState);
        Assert.IsFalse(module.IsEnabled);
    }

    private sealed class TestModule(string id, int priority, ModulePresentation? presentation) : IIslandModule
    {
        public ModuleDescriptor Descriptor { get; } = new(
            id, id, priority, $"{id}.compact", $"{id}.expanded",
            new HashSet<ModuleEventKind> { ModuleEventKind.TimelineChanged }, TimeSpan.FromSeconds(2));
        public ModuleLifecycleState LifecycleState { get; private set; }
        public bool IsEnabled { get; set; } = true;
        public ModulePresentation? CurrentPresentation { get; private set; } = presentation;
        public event EventHandler<ModulePresentation?>? PresentationChanged;
        public event EventHandler<ModuleEvent>? EventOccurred;

        public bool CanExecuteCommand(string commandId) => commandId == "test";

        public ValueTask<bool> ExecuteCommandAsync(
            string commandId,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(CanExecuteCommand(commandId));

        public ValueTask ActivateAsync(CancellationToken cancellationToken = default)
        {
            LifecycleState = ModuleLifecycleState.Active;
            PresentationChanged?.Invoke(this, CurrentPresentation);
            return ValueTask.CompletedTask;
        }

        public ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
        {
            LifecycleState = ModuleLifecycleState.Inactive;
            return ValueTask.CompletedTask;
        }

        public void RaiseEvent(ModuleEventKind kind)
        {
            var current = CurrentPresentation!;
            EventOccurred?.Invoke(this, new ModuleEvent(id, kind, current, TimeSpan.FromSeconds(2), DateTimeOffset.UtcNow));
        }
    }
}
