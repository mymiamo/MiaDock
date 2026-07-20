using MiaDock.Core.Modules;
using MiaDock.Modules.Media;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class ModuleDescriptorTests
{
    [TestMethod]
    public void Constructor_WithValidValues_PreservesModuleMetadata()
    {
        var descriptor = new ModuleDescriptor(
            "timer",
            "Timer",
            10,
            "TimerCompact",
            "TimerExpanded",
            new HashSet<ModuleEventKind> { ModuleEventKind.TimelineChanged },
            TimeSpan.FromSeconds(3));

        Assert.AreEqual("timer", descriptor.Id);
        Assert.AreEqual(10, descriptor.Priority);
        Assert.IsTrue(descriptor.SupportedEvents.Contains(ModuleEventKind.TimelineChanged));
    }

    [TestMethod]
    public void Constructor_WithInvalidDuration_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ModuleDescriptor(
            "timer",
            "Timer",
            10,
            "TimerCompact",
            "TimerExpanded",
            new HashSet<ModuleEventKind>(),
            TimeSpan.Zero));
    }

    [TestMethod]
    public async Task MusicModule_TransitionsLifecycle()
    {
        var module = new MusicModule();

        await module.ActivateAsync();
        Assert.AreEqual(ModuleLifecycleState.Active, module.LifecycleState);

        await module.DeactivateAsync();
        Assert.AreEqual(ModuleLifecycleState.Inactive, module.LifecycleState);
    }
}
