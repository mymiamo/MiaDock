using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiaDock.Modules.Media.Models;
using MiaDock.Modules.Media.Services;

namespace MiaDock.Modules.Media.ViewModels;

public sealed partial class MusicModuleViewModel : ObservableObject, IDisposable
{
    private readonly IFakeMediaService _mediaService;
    private bool _isDisposed;

    public MusicModuleViewModel(IFakeMediaService mediaService)
    {
        _mediaService = mediaService;
        _current = mediaService.Current;
        Scenarios = mediaService.Scenarios;
        _selectedScenario = Scenarios[0];
        _mediaService.SnapshotChanged += OnSnapshotChanged;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousCommand))]
    [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextCommand))]
    private MediaSnapshot _current;

    [ObservableProperty]
    private MediaScenario _selectedScenario;

    public IReadOnlyList<MediaScenario> Scenarios { get; }

    public string PositionText => FormatTime(Current.Position);

    public string RemainingText => $"-{FormatTime(Current.Duration - Current.Position)}";

    public string PlaybackGlyph => Current.PlaybackStatus == PlaybackStatus.Playing ? "\uE769" : "\uE768";

    public double ProgressPercent
    {
        get => Current.Progress * 100;
        set => _mediaService.Seek(value / 100);
    }

    public double VolumePercent
    {
        get => Current.Volume * 100;
        set => _mediaService.SetVolume(value / 100);
    }

    partial void OnSelectedScenarioChanged(MediaScenario value) => _mediaService.SelectScenario(value.Id);

    [RelayCommand(CanExecute = nameof(CanSkipPrevious))]
    private void Previous() => _mediaService.SkipPrevious();

    [RelayCommand(CanExecute = nameof(CanTogglePlayback))]
    private void PlayPause() => _mediaService.TogglePlayback();

    [RelayCommand(CanExecute = nameof(CanSkipNext))]
    private void Next() => _mediaService.SkipNext();

    private bool CanSkipPrevious() => Current.Capabilities.CanSkipPrevious;

    private bool CanTogglePlayback() => Current.PlaybackStatus == PlaybackStatus.Playing
        ? Current.Capabilities.CanPause
        : Current.Capabilities.CanPlay;

    private bool CanSkipNext() => Current.Capabilities.CanSkipNext;

    private void OnSnapshotChanged(object? sender, MediaSnapshot snapshot)
    {
        Current = snapshot;
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(PlaybackGlyph));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(VolumePercent));
    }

    private static string FormatTime(TimeSpan value)
    {
        var safeValue = value < TimeSpan.Zero ? TimeSpan.Zero : value;
        return safeValue.TotalHours >= 1
            ? safeValue.ToString(@"h\:mm\:ss")
            : safeValue.ToString(@"m\:ss");
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _mediaService.SnapshotChanged -= OnSnapshotChanged;
        _isDisposed = true;
    }
}
