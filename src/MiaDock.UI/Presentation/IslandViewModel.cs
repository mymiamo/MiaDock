using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiaDock.Core.Modules;
using MiaDock.Core.Presentation;
using MiaDock.Modules.Media.ViewModels;

namespace MiaDock.UI.Presentation;

public sealed partial class IslandViewModel : ObservableObject, IDisposable
{
    private readonly IIslandStateMachine _stateMachine;
    private readonly IModuleOrchestrator _orchestrator;
    private bool _suppressOrchestratorTransition;

    public IslandViewModel(
        IIslandStateMachine stateMachine,
        MusicModuleViewModel music,
        IModuleOrchestrator orchestrator)
    {
        _stateMachine = stateMachine;
        _orchestrator = orchestrator;
        Music = music;
        CurrentState = stateMachine.CurrentState;
        ActiveModuleDisplay = orchestrator.CurrentDisplay;
        AvailableModules = orchestrator.AvailableModules;
        LastModuleEvent = orchestrator.ActiveEvent;
        orchestrator.CurrentDisplayChanged += OnCurrentDisplayChanged;
        orchestrator.ActiveEventChanged += OnActiveEventChanged;
    }

    public event EventHandler<IslandTransition>? TransitionRequested;

    [ObservableProperty]
    public partial IslandVisualState CurrentState { get; set; }

    public string CurrentStateName => CurrentState.ToString();

    // The media view model remains available to the Phase 1-9 preview window.
    // Overlay module views receive their own data contexts from the view registry.
    public MusicModuleViewModel Music { get; }

    [ObservableProperty]
    public partial ModuleDisplayState? ActiveModuleDisplay { get; set; }

    public ModulePresentation? ActiveModulePresentation => ActiveModuleDisplay?.Presentation;

    public DateTimeOffset? TemporarySelectionExpiresAtUtc =>
        _orchestrator.TemporarySelectionExpiresAtUtc;

    [ObservableProperty]
    public partial IReadOnlyList<ModuleDisplayState> AvailableModules { get; set; }

    public ModuleEvent? LastModuleEvent { get; private set; }

    public TimeSpan ActiveEventDisplayDuration
    {
        get
        {
            if (LastModuleEvent is not { } moduleEvent)
            {
                return TimeSpan.FromSeconds(5);
            }

            var remaining = moduleEvent.ExpiresAtUtc - DateTimeOffset.UtcNow;
            return TimeSpan.FromMilliseconds(Math.Max(
                100,
                Math.Min(moduleEvent.DisplayDuration.TotalMilliseconds, remaining.TotalMilliseconds)));
        }
    }

    partial void OnCurrentStateChanged(IslandVisualState value) => OnPropertyChanged(nameof(CurrentStateName));

    partial void OnActiveModuleDisplayChanged(ModuleDisplayState? value) =>
        OnPropertyChanged(nameof(ActiveModulePresentation));

    [RelayCommand]
    private void ShowCollapsed()
    {
        Dispatch(IslandTrigger.PointerExited);
        Dispatch(IslandTrigger.NotificationElapsed);
        Dispatch(IslandTrigger.CollapseRequested);
    }

    [RelayCommand]
    private void ShowHover()
    {
        Dispatch(IslandTrigger.NotificationElapsed);
        Dispatch(IslandTrigger.CollapseRequested);
        Dispatch(IslandTrigger.PointerEntered);
    }

    [RelayCommand]
    private void ShowExpanded() => HandlePrimaryInvoked();

    [RelayCommand]
    private void ShowNotification()
    {
        Dispatch(IslandTrigger.CollapseRequested);
        Dispatch(IslandTrigger.ModuleEventReceived);
    }

    public void HandlePointerEntered() => Dispatch(IslandTrigger.PointerEntered);

    public void HandlePointerExited() => Dispatch(IslandTrigger.PointerExited);

    public void HandlePrimaryInvoked()
    {
        Dispatch(IslandTrigger.PrimaryInvoked);
    }

    public void HandleCollapseRequested() => Dispatch(IslandTrigger.CollapseRequested);

    public void HandleNotificationElapsed()
    {
        _suppressOrchestratorTransition = true;
        try
        {
            var hasNext = _orchestrator.CompleteActiveEvent();
            Dispatch(hasNext ? IslandTrigger.ModuleEventReceived : IslandTrigger.NotificationElapsed);
        }
        finally
        {
            _suppressOrchestratorTransition = false;
        }
    }

    public void HandleInactivityElapsed()
    {
        Dispatch(IslandTrigger.InactivityElapsed);
    }

    public void SelectPreviousModule() => _orchestrator.MoveSelection(-1);

    public void SelectNextModule() => _orchestrator.MoveSelection(1);

    public void SelectModule(string moduleId) => _orchestrator.SelectModule(moduleId);

    public void SelectDefault() => _orchestrator.SelectDefault();

    public void EndManualSelection() => _orchestrator.EndManualSelection();

    public bool NotifyModuleInteraction()
    {
        var updated = _orchestrator.NotifyUserInteraction();
        if (updated)
        {
            OnPropertyChanged(nameof(TemporarySelectionExpiresAtUtc));
        }

        return updated;
    }

    public void UpdateTemporarySelectionDuration(TimeSpan duration)
    {
        _orchestrator.UpdateTemporarySelectionDuration(duration);
        OnPropertyChanged(nameof(TemporarySelectionExpiresAtUtc));
    }

    public bool ExpireTemporarySelection()
    {
        var expired = _orchestrator.ExpireTemporarySelection();
        if (expired)
        {
            OnPropertyChanged(nameof(TemporarySelectionExpiresAtUtc));
        }

        return expired;
    }

    private void Dispatch(IslandTrigger trigger)
    {
        var transition = _stateMachine.Dispatch(trigger);
        CurrentState = transition.CurrentState;
        TransitionRequested?.Invoke(this, transition);
    }

    private void OnCurrentDisplayChanged(object? sender, ModuleDisplayState? display)
    {
        ActiveModuleDisplay = display;
        var availableModules = _orchestrator.AvailableModules;
        if (!HaveSameNavigationItems(AvailableModules, availableModules))
        {
            AvailableModules = availableModules;
        }
        OnPropertyChanged(nameof(TemporarySelectionExpiresAtUtc));
    }

    private static bool HaveSameNavigationItems(
        IReadOnlyList<ModuleDisplayState> current,
        IReadOnlyList<ModuleDisplayState> next)
    {
        if (ReferenceEquals(current, next))
        {
            return true;
        }

        if (current.Count != next.Count)
        {
            return false;
        }

        for (var index = 0; index < current.Count; index++)
        {
            if (!string.Equals(
                    current[index].Descriptor.Id,
                    next[index].Descriptor.Id,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private void OnActiveEventChanged(object? sender, ModuleEvent? moduleEvent)
    {
        LastModuleEvent = moduleEvent;
        OnPropertyChanged(nameof(LastModuleEvent));
        OnPropertyChanged(nameof(ActiveEventDisplayDuration));
        if (moduleEvent is not null && !_suppressOrchestratorTransition)
        {
            Dispatch(IslandTrigger.ModuleEventReceived);
        }
    }

    public void Dispose()
    {
        _orchestrator.CurrentDisplayChanged -= OnCurrentDisplayChanged;
        _orchestrator.ActiveEventChanged -= OnActiveEventChanged;
    }
}
