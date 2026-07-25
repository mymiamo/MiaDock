using MiaDock.Core.Modules;
using System.Diagnostics;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class ModuleOrchestratorTests
{
    [TestMethod]
    public async Task PersistentDisplay_UsesHighestPersistentPriority()
    {
        var lower = new FakeModule("media", 100);
        var higher = new FakeModule("timer", 300);
        await using var registry = new IslandModuleRegistry([lower, higher]);
        using var orchestrator = new ModuleOrchestrator(registry);
        await registry.InitializeAsync();

        Assert.AreEqual("timer", orchestrator.CurrentDisplay?.Descriptor.Id);
    }

    [TestMethod]
    public async Task MatchingCoalescingKey_ReplacesActiveEvent()
    {
        var module = new FakeModule("media", 100);
        await using var registry = new IslandModuleRegistry([module]);
        using var orchestrator = new ModuleOrchestrator(registry);
        await registry.InitializeAsync();

        module.PublishEvent("first", ModuleEventPriority.Normal, "media:track");
        module.PublishEvent("second", ModuleEventPriority.Normal, "media:track");

        Assert.AreEqual("second", orchestrator.ActiveEvent?.Presentation.PrimaryText);
        Assert.AreEqual(0, orchestrator.PendingEventCount);
    }

    [TestMethod]
    public async Task HigherPriority_PreemptsAndReturnsToInterruptedEvent()
    {
        var module = new FakeModule("system", 100);
        await using var registry = new IslandModuleRegistry([module]);
        using var orchestrator = new ModuleOrchestrator(registry);
        await registry.InitializeAsync();

        module.PublishEvent("normal", ModuleEventPriority.Normal, "normal");
        module.PublishEvent("critical", ModuleEventPriority.Critical, "critical");

        Assert.AreEqual("critical", orchestrator.ActiveEvent?.Presentation.PrimaryText);
        Assert.AreEqual(1, orchestrator.PendingEventCount);
        Assert.IsTrue(orchestrator.CompleteActiveEvent());
        Assert.AreEqual("normal", orchestrator.ActiveEvent?.Presentation.PrimaryText);
    }

    [TestMethod]
    public async Task LowerPriorityQueuedEvent_DoesNotRestartActiveNotification()
    {
        var module = new FakeModule("system", 100);
        await using var registry = new IslandModuleRegistry([module]);
        using var orchestrator = new ModuleOrchestrator(registry);
        await registry.InitializeAsync();
        var activeChangeCount = 0;
        orchestrator.ActiveEventChanged += (_, _) => activeChangeCount++;

        module.PublishEvent("active", ModuleEventPriority.High, "active");
        module.PublishEvent("queued", ModuleEventPriority.Low, "queued");

        Assert.AreEqual(1, activeChangeCount);
        Assert.AreEqual("active", orchestrator.ActiveEvent?.Presentation.PrimaryText);
        Assert.AreEqual(1, orchestrator.PendingEventCount);
    }

    [TestMethod]
    public async Task CoalescedPendingEvent_PreemptsWhenItsPriorityBecomesCritical()
    {
        var module = new FakeModule("system", 100);
        await using var registry = new IslandModuleRegistry([module]);
        using var orchestrator = new ModuleOrchestrator(registry);
        await registry.InitializeAsync();

        module.PublishEvent("active", ModuleEventPriority.High, "active");
        module.PublishEvent("queued", ModuleEventPriority.Low, "same-key");
        module.PublishEvent("upgraded", ModuleEventPriority.Critical, "same-key");

        Assert.AreEqual("upgraded", orchestrator.ActiveEvent?.Presentation.PrimaryText);
        Assert.AreEqual(1, orchestrator.PendingEventCount);
        orchestrator.CompleteActiveEvent();
        Assert.AreEqual("active", orchestrator.ActiveEvent?.Presentation.PrimaryText);
    }

    [TestMethod]
    public async Task EqualPriority_PreservesArrivalOrder()
    {
        var module = new FakeModule("system", 100);
        await using var registry = new IslandModuleRegistry([module]);
        using var orchestrator = new ModuleOrchestrator(registry);
        await registry.InitializeAsync();

        module.PublishEvent("first", ModuleEventPriority.High, "first");
        module.PublishEvent("second", ModuleEventPriority.High, "second");
        module.PublishEvent("third", ModuleEventPriority.High, "third");

        orchestrator.CompleteActiveEvent();
        Assert.AreEqual("second", orchestrator.ActiveEvent?.Presentation.PrimaryText);
        orchestrator.CompleteActiveEvent();
        Assert.AreEqual("third", orchestrator.ActiveEvent?.Presentation.PrimaryText);
    }

    [TestMethod]
    public async Task ExpiredEvent_IsNotShown()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-22T12:00:00Z"));
        var module = new FakeModule("system", 100, clock);
        await using var registry = new IslandModuleRegistry([module]);
        using var orchestrator = new ModuleOrchestrator(registry, clock);
        await registry.InitializeAsync();

        module.PublishEvent(
            "expired",
            ModuleEventPriority.Normal,
            "expired",
            clock.GetUtcNow().AddSeconds(-1));

        Assert.IsNull(orchestrator.ActiveEvent);
        Assert.AreEqual(0, orchestrator.PendingEventCount);
    }

    [TestMethod]
    public async Task Queue_IsBoundedAtThirtyTwoEvents()
    {
        var module = new FakeModule("system", 100);
        await using var registry = new IslandModuleRegistry([module]);
        using var orchestrator = new ModuleOrchestrator(registry);
        await registry.InitializeAsync();
        module.PublishEvent("active", ModuleEventPriority.Critical, "active");

        for (var index = 0; index < 50; index++)
        {
            module.PublishEvent($"event-{index}", ModuleEventPriority.Low, $"event-{index}");
        }

        Assert.AreEqual(ModuleOrchestrator.MaximumPendingEvents, orchestrator.PendingEventCount);
    }

    [TestMethod]
    public async Task ManualSelection_CyclesAndReturnsToPersistentDisplay()
    {
        var media = new FakeModule("media", 100);
        var timer = new FakeModule("timer", 300);
        await using var registry = new IslandModuleRegistry([media, timer]);
        using var orchestrator = new ModuleOrchestrator(registry);
        await registry.InitializeAsync();

        Assert.AreEqual("timer", orchestrator.CurrentDisplay?.Descriptor.Id);
        Assert.IsTrue(orchestrator.MoveSelection(1));
        Assert.AreEqual("media", orchestrator.CurrentDisplay?.Descriptor.Id);

        orchestrator.EndManualSelection();
        Assert.AreEqual("timer", orchestrator.CurrentDisplay?.Descriptor.Id);
    }

    [TestMethod]
    public async Task NonPersistentModule_IsAvailableButDoesNotReplaceNeutralCapsule()
    {
        var system = new FakeModule("system", 0, isPersistent: false);
        await using var registry = new IslandModuleRegistry([system]);
        using var orchestrator = new ModuleOrchestrator(registry);
        await registry.InitializeAsync();

        Assert.HasCount(1, orchestrator.AvailableModules);
        Assert.IsNull(orchestrator.CurrentDisplay);
        Assert.IsTrue(orchestrator.SelectModule("system"));
        Assert.AreEqual("system", orchestrator.CurrentDisplay?.Descriptor.Id);
        orchestrator.EndManualSelection();
        Assert.IsNull(orchestrator.CurrentDisplay);
    }

    [TestMethod]
    public async Task Carousel_IncludesNeutralCapsuleBetweenModules()
    {
        var media = new FakeModule("media", 100);
        var timer = new FakeModule("timer", 300);
        await using var registry = new IslandModuleRegistry([media, timer]);
        using var orchestrator = new ModuleOrchestrator(registry);
        await registry.InitializeAsync();

        Assert.IsTrue(orchestrator.SelectDefault());
        Assert.IsNull(orchestrator.CurrentDisplay);
        Assert.IsTrue(orchestrator.MoveSelection(1));
        Assert.IsNotNull(orchestrator.CurrentDisplay);

        orchestrator.EndManualSelection();
        Assert.AreEqual("timer", orchestrator.CurrentDisplay?.Descriptor.Id);
    }

    [TestMethod]
    public async Task ManualClockAndTimerSelections_ReturnAfterTemporaryEvents()
    {
        var media = new FakeModule("media", 100);
        var timer = new FakeModule("timer", 300);
        await using var registry = new IslandModuleRegistry([media, timer]);
        using var orchestrator = new ModuleOrchestrator(registry);
        await registry.InitializeAsync();

        Assert.IsTrue(orchestrator.SelectDefault());
        media.PublishEvent("track", ModuleEventPriority.High, "track");
        Assert.AreEqual("media", orchestrator.CurrentDisplay?.Descriptor.Id);
        orchestrator.CompleteActiveEvent();
        Assert.IsNull(orchestrator.CurrentDisplay);

        Assert.IsTrue(orchestrator.SelectModule("timer"));
        media.PublishEvent("track-2", ModuleEventPriority.High, "track-2");
        Assert.AreEqual("media", orchestrator.CurrentDisplay?.Descriptor.Id);
        orchestrator.CompleteActiveEvent();
        Assert.AreEqual("timer", orchestrator.CurrentDisplay?.Descriptor.Id);
    }

    [TestMethod]
    public async Task PassiveManualSelection_ReturnsToDefaultAfterEightSeconds()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-24T12:00:00Z"));
        var module = new FakeModule("timer", 300, clock, isPersistent: false);
        await using var registry = new IslandModuleRegistry([module]);
        using var orchestrator = new ModuleOrchestrator(registry, clock);
        await registry.InitializeAsync();
        var returnedToDefault = false;
        orchestrator.CurrentDisplayChanged += (_, display) =>
            returnedToDefault |= display is null;

        Assert.AreEqual(TimeSpan.FromSeconds(8), orchestrator.TemporarySelectionDuration);
        Assert.IsTrue(orchestrator.SelectModule("timer"));
        Assert.AreEqual(
            clock.GetUtcNow().AddSeconds(8),
            orchestrator.TemporarySelectionExpiresAtUtc);

        clock.Advance(TimeSpan.FromMilliseconds(7_999));
        Assert.IsFalse(orchestrator.ExpireTemporarySelection());
        Assert.AreEqual("timer", orchestrator.CurrentDisplay?.Descriptor.Id);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.IsTrue(orchestrator.ExpireTemporarySelection());
        Assert.IsNull(orchestrator.CurrentDisplay);
        Assert.IsTrue(returnedToDefault);
    }

    [TestMethod]
    public async Task UserInteraction_RenewsPassiveSelectionDuration()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-24T12:00:00Z"));
        var module = new FakeModule("timer", 300, clock, isPersistent: false);
        await using var registry = new IslandModuleRegistry([module]);
        using var orchestrator = new ModuleOrchestrator(registry, clock);
        await registry.InitializeAsync();

        Assert.IsTrue(orchestrator.SelectModule("timer"));
        clock.Advance(TimeSpan.FromSeconds(7));
        Assert.IsTrue(orchestrator.NotifyUserInteraction());

        clock.Advance(TimeSpan.FromSeconds(7));
        Assert.AreEqual("timer", orchestrator.CurrentDisplay?.Descriptor.Id);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.IsTrue(orchestrator.ExpireTemporarySelection());
        Assert.IsNull(orchestrator.CurrentDisplay);
        Assert.IsFalse(orchestrator.NotifyUserInteraction());
    }

    [TestMethod]
    public async Task PersistentManualSelection_DoesNotReturnToDefault()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-24T12:00:00Z"));
        var media = new FakeModule("media", 100, clock);
        var automaticFallback = new FakeModule("fallback", 500, clock);
        await using var registry = new IslandModuleRegistry([media, automaticFallback]);
        using var orchestrator = new ModuleOrchestrator(registry, clock);
        await registry.InitializeAsync();

        Assert.IsTrue(orchestrator.SelectModule("media"));
        Assert.IsFalse(orchestrator.NotifyUserInteraction());
        clock.Advance(TimeSpan.FromMinutes(5));

        Assert.AreEqual("media", orchestrator.CurrentDisplay?.Descriptor.Id);
    }

    [TestMethod]
    public async Task ActiveWork_KeepsSelectionUntilPresentationBecomesPassive()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-24T12:00:00Z"));
        var timer = new FakeModule("timer", 300, clock, isPersistent: false);
        timer.SetActiveWork(true);
        await using var registry = new IslandModuleRegistry([timer]);
        using var orchestrator = new ModuleOrchestrator(registry, clock);
        await registry.InitializeAsync();

        Assert.IsTrue(orchestrator.SelectModule("timer"));
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.AreEqual("timer", orchestrator.CurrentDisplay?.Descriptor.Id);

        timer.SetActiveWork(false);
        clock.Advance(TimeSpan.FromMilliseconds(7_999));
        Assert.AreEqual("timer", orchestrator.CurrentDisplay?.Descriptor.Id);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.IsTrue(orchestrator.ExpireTemporarySelection());
        Assert.IsNull(orchestrator.CurrentDisplay);
    }

    [TestMethod]
    public async Task NotificationCompletion_RestoresPreviousPersistentTarget()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-24T12:00:00Z"));
        var media = new FakeModule("media", 100, clock);
        var system = new FakeModule("system", 300, clock, isPersistent: false);
        await using var registry = new IslandModuleRegistry([media, system]);
        using var orchestrator = new ModuleOrchestrator(registry, clock);
        await registry.InitializeAsync();

        Assert.IsTrue(orchestrator.SelectModule("media"));
        system.PublishEvent("network changed", ModuleEventPriority.High, "system:network");
        Assert.AreEqual("system", orchestrator.CurrentDisplay?.Descriptor.Id);
        orchestrator.CompleteActiveEvent();
        Assert.AreEqual("media", orchestrator.CurrentDisplay?.Descriptor.Id);

        Assert.IsTrue(orchestrator.SelectDefault());
        system.PublishEvent("battery changed", ModuleEventPriority.High, "system:battery");
        Assert.AreEqual("system", orchestrator.CurrentDisplay?.Descriptor.Id);
        orchestrator.CompleteActiveEvent();
        Assert.IsNull(orchestrator.CurrentDisplay);
    }

    [TestMethod]
    public async Task Notifications_SuspendPassiveSelectionUntilEntireQueueCompletes()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-24T12:00:00Z"));
        var timer = new FakeModule("timer", 300, clock, isPersistent: false);
        var system = new FakeModule("system", 200, clock, isPersistent: false);
        await using var registry = new IslandModuleRegistry([timer, system]);
        using var orchestrator = new ModuleOrchestrator(registry, clock);
        await registry.InitializeAsync();

        Assert.IsTrue(orchestrator.SelectModule("timer"));
        clock.Advance(TimeSpan.FromSeconds(7));
        system.PublishEvent("first", ModuleEventPriority.High, "system:first");
        system.PublishEvent("second", ModuleEventPriority.Low, "system:second");

        Assert.IsNull(orchestrator.TemporarySelectionExpiresAtUtc);
        Assert.IsTrue(orchestrator.NotifyUserInteraction());
        Assert.IsNull(orchestrator.TemporarySelectionExpiresAtUtc);

        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.IsFalse(orchestrator.ExpireTemporarySelection());
        Assert.AreEqual("system", orchestrator.CurrentDisplay?.Descriptor.Id);

        Assert.IsTrue(orchestrator.CompleteActiveEvent());
        Assert.IsNull(orchestrator.TemporarySelectionExpiresAtUtc);
        Assert.AreEqual("second", orchestrator.ActiveEvent?.Presentation.PrimaryText);

        Assert.IsFalse(orchestrator.CompleteActiveEvent());
        Assert.AreEqual(
            clock.GetUtcNow().AddSeconds(8),
            orchestrator.TemporarySelectionExpiresAtUtc);
        Assert.AreEqual("timer", orchestrator.CurrentDisplay?.Descriptor.Id);

        clock.Advance(TimeSpan.FromSeconds(8));
        Assert.IsTrue(orchestrator.ExpireTemporarySelection());
        Assert.IsNull(orchestrator.CurrentDisplay);
    }

    [TestMethod]
    public async Task ExplicitFalsePersistenceOverride_MakesPersistentDescriptorTemporary()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-24T12:00:00Z"));
        var media = new FakeModule("media", 100, clock);
        media.SetPersistenceOverride(false);
        await using var registry = new IslandModuleRegistry([media]);
        using var orchestrator = new ModuleOrchestrator(registry, clock);
        await registry.InitializeAsync();

        Assert.IsTrue(orchestrator.SelectModule("media"));
        Assert.AreEqual(
            clock.GetUtcNow().AddSeconds(8),
            orchestrator.TemporarySelectionExpiresAtUtc);

        clock.Advance(TimeSpan.FromSeconds(8));
        Assert.IsTrue(orchestrator.ExpireTemporarySelection());
        Assert.IsNull(orchestrator.CurrentDisplay);
    }

    [TestMethod]
    public async Task TemporarySelectionDuration_CanBeUpdatedWithinSettingsRange()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-24T12:00:00Z"));
        var module = new FakeModule("timer", 300, clock, isPersistent: false);
        await using var registry = new IslandModuleRegistry([module]);
        using var orchestrator = new ModuleOrchestrator(registry, clock);
        await registry.InitializeAsync();

        orchestrator.UpdateTemporarySelectionDuration(TimeSpan.FromSeconds(3));
        Assert.AreEqual(TimeSpan.FromSeconds(3), orchestrator.TemporarySelectionDuration);
        Assert.IsTrue(orchestrator.SelectModule("timer"));

        clock.Advance(TimeSpan.FromMilliseconds(2_999));
        Assert.AreEqual("timer", orchestrator.CurrentDisplay?.Descriptor.Id);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.IsTrue(orchestrator.ExpireTemporarySelection());
        Assert.IsNull(orchestrator.CurrentDisplay);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            orchestrator.UpdateTemporarySelectionDuration(TimeSpan.FromSeconds(2)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            orchestrator.UpdateTemporarySelectionDuration(TimeSpan.FromSeconds(31)));
    }

    [TestMethod]
    public async Task FiftyThousandEvents_RemainBoundedAndResponsive()
    {
        var module = new FakeModule("stress", 100);
        await using var registry = new IslandModuleRegistry([module]);
        using var orchestrator = new ModuleOrchestrator(registry);
        await registry.InitializeAsync();
        module.PublishEvent("critical", ModuleEventPriority.Critical, "active");

        var stopwatch = Stopwatch.StartNew();
        for (var index = 0; index < 50_000; index++)
        {
            module.PublishEvent(
                $"event-{index}",
                index % 11 == 0 ? ModuleEventPriority.High : ModuleEventPriority.Low,
                $"coalescing-{index % 64}");
        }
        stopwatch.Stop();

        Assert.IsLessThanOrEqualTo(ModuleOrchestrator.MaximumPendingEvents, orchestrator.PendingEventCount);
        Assert.AreEqual(50_001, orchestrator.Statistics.ReceivedEvents);
        Assert.IsGreaterThan(0, orchestrator.Statistics.CoalescedEvents);
        Assert.IsGreaterThan(0, orchestrator.Statistics.DroppedEvents);
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"50,000 events took {stopwatch.Elapsed}.");
    }

    [TestMethod]
    public async Task Dispose_DetachesRegistryCallbacks()
    {
        var module = new FakeModule("lifecycle", 100);
        await using var registry = new IslandModuleRegistry([module]);
        var orchestrator = new ModuleOrchestrator(registry);
        await registry.InitializeAsync();
        module.PublishEvent("before", ModuleEventPriority.Normal, "before");
        var beforeDispose = orchestrator.Statistics.ReceivedEvents;

        orchestrator.Dispose();
        module.PublishEvent("after", ModuleEventPriority.Critical, "after");

        Assert.AreEqual(beforeDispose, orchestrator.Statistics.ReceivedEvents);
    }

    private sealed class FakeModule : IIslandModule
    {
        private readonly TimeProvider _timeProvider;
        private bool? _persistenceOverride;

        public FakeModule(
            string id,
            int persistentPriority,
            TimeProvider? timeProvider = null,
            bool isPersistent = true)
        {
            _timeProvider = timeProvider ?? TimeProvider.System;
            Descriptor = new ModuleDescriptor(
                id,
                id,
                persistentPriority,
                $"{id}.compact",
                $"{id}.expanded",
                new HashSet<ModuleEventKind> { ModuleEventKind.PlaybackChanged },
                TimeSpan.FromSeconds(5),
                notificationViewKey: $"{id}.notification",
                persistentPriority: persistentPriority,
                isPersistent: isPersistent);
            CurrentPresentation = CreatePresentation(id);
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
            PresentationChanged?.Invoke(this, CurrentPresentation);
            return ValueTask.CompletedTask;
        }

        public ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
        {
            LifecycleState = ModuleLifecycleState.Inactive;
            return ValueTask.CompletedTask;
        }

        public void PublishEvent(
            string title,
            ModuleEventPriority priority,
            string key,
            DateTimeOffset? expiresAt = null)
        {
            CurrentPresentation = CreatePresentation(title);
            PresentationChanged?.Invoke(this, CurrentPresentation);
            EventOccurred?.Invoke(this, new ModuleEvent(
                Descriptor.Id,
                ModuleEventKind.PlaybackChanged,
                CurrentPresentation,
                TimeSpan.FromSeconds(5),
                _timeProvider.GetUtcNow(),
                priority,
                key,
                expiresAt));
        }

        public void SetActiveWork(bool hasActiveWork)
        {
            _persistenceOverride = hasActiveWork ? true : null;
            CurrentPresentation = CreatePresentation(Descriptor.Id);
            PresentationChanged?.Invoke(this, CurrentPresentation);
        }

        public void SetPersistenceOverride(bool? persistenceOverride)
        {
            _persistenceOverride = persistenceOverride;
            CurrentPresentation = CreatePresentation(Descriptor.Id);
            PresentationChanged?.Invoke(this, CurrentPresentation);
        }

        private ModulePresentation CreatePresentation(string title) => new(
            Descriptor.Id,
            title,
            string.Empty,
            "\uE7C3",
            ModuleIndicatorKind.None,
            isPersistentOverride: _persistenceOverride);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public FakeTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(duration));
            }

            _now = _now.Add(duration);
        }
    }
}
