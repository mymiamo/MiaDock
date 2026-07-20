using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiaDock.Core.Presentation;
using MiaDock.Modules.Media.ViewModels;

namespace MiaDock.UI.Presentation;

public sealed partial class IslandViewModel : ObservableObject
{
    private readonly IIslandStateMachine _stateMachine;

    public IslandViewModel(IIslandStateMachine stateMachine, MusicModuleViewModel music)
    {
        _stateMachine = stateMachine;
        Music = music;
        CurrentState = stateMachine.CurrentState;
    }

    [ObservableProperty]
    public partial IslandVisualState CurrentState { get; set; }

    public string CurrentStateName => CurrentState.ToString();

    public MusicModuleViewModel Music { get; }

    partial void OnCurrentStateChanged(IslandVisualState value) => OnPropertyChanged(nameof(CurrentStateName));

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
    private void ShowExpanded() => Dispatch(IslandTrigger.PrimaryInvoked);

    [RelayCommand]
    private void ShowNotification()
    {
        Dispatch(IslandTrigger.CollapseRequested);
        Dispatch(IslandTrigger.TrackChanged);
    }

    private void Dispatch(IslandTrigger trigger) => CurrentState = _stateMachine.Dispatch(trigger).CurrentState;
}
