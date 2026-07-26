using MiaDock.Core.Modules;
using MiaDock.Core.Presentation;
using MiaDock.Modules.Media;
using MiaDock.Modules.Media.Services;
using MiaDock.Modules.Media.ViewModels;
using MiaDock.UI.Presentation;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class IslandViewModelTests
{
    [TestMethod]
    public void PointerHandlers_UpdateVisualState()
    {
        using var context = new TestContext();

        context.ViewModel.HandlePointerEntered();
        Assert.AreEqual(IslandVisualState.Hover, context.ViewModel.CurrentState);

        context.ViewModel.HandlePointerExited();
        Assert.AreEqual(IslandVisualState.Collapsed, context.ViewModel.CurrentState);
    }

    [TestMethod]
    public void PrimaryInvocation_ExpandsModule()
    {
        using var context = new TestContext();

        context.ViewModel.HandlePrimaryInvoked();

        Assert.AreEqual(IslandVisualState.ExpandedModule, context.ViewModel.CurrentState);
    }

    [TestMethod]
    public void RealTrackChange_RefreshesContentWithoutOpeningNotification()
    {
        using var context = new TestContext();
        IslandTransition? published = null;
        context.ViewModel.TransitionRequested += (_, transition) => published = transition;

        context.Service.SkipNext();

        Assert.AreEqual(IslandVisualState.Collapsed, context.ViewModel.CurrentState);
        Assert.IsNull(published);
        Assert.AreEqual(context.Service.Current.Track.Title,
            context.ViewModel.ActiveModulePresentation?.PrimaryText);
    }

    [TestMethod]
    public void RepeatedTrackChanges_DoNotPublishVisualTransitions()
    {
        using var context = new TestContext();
        var transitions = new List<IslandTransition>();
        context.ViewModel.TransitionRequested += (_, transition) => transitions.Add(transition);

        context.Service.SkipNext();
        context.Service.SkipNext();

        Assert.IsEmpty(transitions);
        Assert.AreEqual(IslandVisualState.Collapsed, context.ViewModel.CurrentState);
    }

    [TestMethod]
    public void RapidTrackChanges_RefreshWithoutOpeningTheDock()
    {
        using var context = new TestContext();
        var transitionCount = 0;
        context.ViewModel.TransitionRequested += (_, transition) =>
        {
            if (transition.Trigger == IslandTrigger.ModuleEventReceived)
            {
                transitionCount++;
            }
        };

        for (var index = 0; index < 100; index++)
        {
            context.Service.SelectScenario(index % 2 == 0 ? "long-text" : "missing-artwork");
        }

        Assert.AreEqual(0, transitionCount);
        Assert.AreEqual(IslandVisualState.Collapsed, context.ViewModel.CurrentState);
        Assert.IsNotNull(context.ViewModel.ActiveModulePresentation);
    }

    [TestMethod]
    public void PrimaryInvocation_FromNeutralCapsule_ExpandsDefaultDockWithoutSelectingModule()
    {
        using var music = new MusicModuleViewModel(new FakeMediaService());
        var orchestrator = new NeutralOrchestrator();
        var viewModel = new IslandViewModel(new IslandStateMachine(), music, orchestrator);

        viewModel.HandlePrimaryInvoked();

        Assert.IsNull(orchestrator.SelectedModuleId);
        Assert.IsNull(viewModel.ActiveModuleDisplay);
        Assert.AreEqual(IslandVisualState.ExpandedModule, viewModel.CurrentState);
    }

    [TestMethod]
    public void ManualModuleSelection_SurvivesCollapseAndInactivity()
    {
        using var music = new MusicModuleViewModel(new FakeMediaService());
        var orchestrator = new NeutralOrchestrator();
        var viewModel = new IslandViewModel(new IslandStateMachine(), music, orchestrator);
        viewModel.SelectModule("system");

        viewModel.HandlePrimaryInvoked();
        viewModel.HandlePrimaryInvoked();
        viewModel.HandleInactivityElapsed();

        Assert.AreEqual("system", orchestrator.SelectedModuleId);
        Assert.AreEqual(0, orchestrator.EndManualSelectionCount);
    }

    [TestMethod]
    public void TemporarySelectionScheduler_ForwardsInteractionSettingsAndExpiration()
    {
        using var music = new MusicModuleViewModel(new FakeMediaService());
        var orchestrator = new NeutralOrchestrator();
        var viewModel = new IslandViewModel(new IslandStateMachine(), music, orchestrator);

        viewModel.UpdateTemporarySelectionDuration(TimeSpan.FromSeconds(12));
        var refreshed = viewModel.NotifyModuleInteraction();
        var expired = viewModel.ExpireTemporarySelection();

        Assert.AreEqual(TimeSpan.FromSeconds(12), orchestrator.UpdatedDuration);
        Assert.IsTrue(refreshed);
        Assert.IsTrue(expired);
        Assert.AreEqual(1, orchestrator.NotifyInteractionCount);
        Assert.AreEqual(1, orchestrator.ExpireSelectionCount);
    }

    [TestMethod]
    public void Dispose_DetachesOrchestratorEvents()
    {
        using var music = new MusicModuleViewModel(new FakeMediaService());
        var orchestrator = new NeutralOrchestrator();
        var viewModel = new IslandViewModel(new IslandStateMachine(), music, orchestrator);

        Assert.AreEqual(1, orchestrator.CurrentDisplaySubscriberCount);
        Assert.AreEqual(1, orchestrator.ActiveEventSubscriberCount);

        viewModel.Dispose();

        Assert.AreEqual(0, orchestrator.CurrentDisplaySubscriberCount);
        Assert.AreEqual(0, orchestrator.ActiveEventSubscriberCount);
    }

    private sealed class TestContext : IDisposable
    {
        private readonly MusicModule _module;
        private readonly IslandModuleRegistry _registry;
        private readonly ModuleOrchestrator _orchestrator;

        public TestContext()
        {
            Service = new FakeMediaService();
            Music = new MusicModuleViewModel(Service);
            _module = new MusicModule(Music);
            _registry = new IslandModuleRegistry([_module]);
            _orchestrator = new ModuleOrchestrator(_registry);
            _registry.InitializeAsync().AsTask().GetAwaiter().GetResult();
            ViewModel = new IslandViewModel(new IslandStateMachine(), Music, _orchestrator);
        }

        public FakeMediaService Service { get; }
        public MusicModuleViewModel Music { get; }
        public IslandViewModel ViewModel { get; }

        public void Dispose()
        {
            ViewModel.Dispose();
            _orchestrator.Dispose();
            _registry.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _module.Dispose();
            Music.Dispose();
        }
    }

    private sealed class NeutralOrchestrator : IModuleOrchestrator
    {
        private static readonly ModuleDescriptor Descriptor = new(
            "system",
            "System",
            10,
            "compact",
            "expanded",
            new HashSet<ModuleEventKind> { ModuleEventKind.StatusChanged },
            TimeSpan.FromSeconds(3),
            isPersistent: false);
        private static readonly ModuleDisplayState Display = new(
            Descriptor,
            new ModulePresentation(
                "system", "System", string.Empty, "\uE7F8", ModuleIndicatorKind.None));

        public IReadOnlyList<ModuleDisplayState> AvailableModules { get; } = [Display];
        public ModuleDisplayState? CurrentDisplay { get; private set; }
        public ModuleEvent? ActiveEvent => null;
        public int PendingEventCount => 0;
        public string? SelectedModuleId { get; private set; }
        public int EndManualSelectionCount { get; private set; }
        public int NotifyInteractionCount { get; private set; }
        public int ExpireSelectionCount { get; private set; }
        public int CurrentDisplaySubscriberCount { get; private set; }
        public int ActiveEventSubscriberCount { get; private set; }
        public TimeSpan UpdatedDuration { get; private set; }
        private EventHandler<ModuleDisplayState?>? _currentDisplayChanged;
        public event EventHandler<ModuleDisplayState?>? CurrentDisplayChanged
        {
            add
            {
                _currentDisplayChanged += value;
                CurrentDisplaySubscriberCount++;
            }
            remove
            {
                _currentDisplayChanged -= value;
                CurrentDisplaySubscriberCount--;
            }
        }
        public event EventHandler<ModuleEvent?>? ActiveEventChanged
        {
            add => ActiveEventSubscriberCount++;
            remove => ActiveEventSubscriberCount--;
        }

        public bool SelectModule(string moduleId)
        {
            SelectedModuleId = moduleId;
            CurrentDisplay = Display;
            _currentDisplayChanged?.Invoke(this, Display);
            return true;
        }

        public bool SelectDefault()
        {
            SelectedModuleId = null;
            CurrentDisplay = null;
            _currentDisplayChanged?.Invoke(this, null);
            return true;
        }

        public bool MoveSelection(int offset) => false;
        public void EndManualSelection()
        {
            EndManualSelectionCount++;
            CurrentDisplay = null;
        }
        public bool NotifyUserInteraction()
        {
            NotifyInteractionCount++;
            return true;
        }
        public void UpdateTemporarySelectionDuration(TimeSpan duration) => UpdatedDuration = duration;
        public bool ExpireTemporarySelection()
        {
            ExpireSelectionCount++;
            return true;
        }
        public bool CompleteActiveEvent() => false;
        public void Dispose() { }
    }
}
