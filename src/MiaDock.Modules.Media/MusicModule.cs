using MiaDock.Core.Modules;
using MiaDock.Core.Localization;
using MiaDock.Modules.Media.ViewModels;
using System.ComponentModel;

namespace MiaDock.Modules.Media;

public sealed class MusicModule : IIslandModule, IDisposable
{
    private readonly MusicModuleViewModel? _viewModel;
    private readonly ILocalizationService? _localization;
    private bool _isEnabled = true;

    public MusicModule(
        MusicModuleViewModel? viewModel = null,
        ILocalizationService? localization = null)
    {
        _viewModel = viewModel;
        _localization = localization;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
        if (_localization is not null)
        {
            _localization.LanguageChanged += OnLanguageChanged;
        }
    }

    public ModuleDescriptor Descriptor { get; } = new(
        "media",
        "Music",
        100,
        "MusicCompactView",
        "MusicExpandedView",
        new HashSet<ModuleEventKind>
        {
            ModuleEventKind.PlaybackChanged,
            ModuleEventKind.TimelineChanged
        },
        TimeSpan.FromSeconds(5),
        [
            new ModuleCommandDescriptor("previous", "Önceki", "\uE892"),
            new ModuleCommandDescriptor("play-pause", "Oynat veya duraklat", "\uE768"),
            new ModuleCommandDescriptor("next", "Sonraki", "\uE893")
        ],
        "MusicNotificationView",
        100,
        hoverViewKey: "MusicHoverView",
        iconGlyph: "\uE8D6");

    public ModuleLifecycleState LifecycleState { get; private set; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            PresentationChanged?.Invoke(this, CurrentPresentation);
        }
    }

    public ModulePresentation? CurrentPresentation => _viewModel?.IsMediaAvailable == true
        ? new ModulePresentation(
            Descriptor.Id,
            _viewModel.Current.Track.Title,
            _viewModel.Current.Track.Artist,
            "\uE8D6",
            ModuleIndicatorKind.ActivityBars,
            presentationKind: ModulePresentationKind.Media,
            commands:
            [
                new ModuleCommandState("previous", Text("Dock.PreviousTrack", "Önceki parça"), "\uE892", CanExecuteCommand("previous")),
                new ModuleCommandState("play-pause", Text("Dock.PlayPause", "Oynat veya duraklat"), "\uE768", CanExecuteCommand("play-pause")),
                new ModuleCommandState("next", Text("Dock.NextTrack", "Sonraki parça"), "\uE893", CanExecuteCommand("next"))
            ])
        : null;

    public event EventHandler<ModulePresentation?>? PresentationChanged;

    public event EventHandler<ModuleEvent>? EventOccurred
    {
        add { }
        remove { }
    }

    public bool CanExecuteCommand(string commandId) => commandId switch
    {
        "previous" => _viewModel?.PreviousCommand.CanExecute(null) == true,
        "play-pause" => _viewModel?.PlayPauseCommand.CanExecute(null) == true,
        "next" => _viewModel?.NextCommand.CanExecute(null) == true,
        _ => false
    };

    public async ValueTask<bool> ExecuteCommandAsync(
        string commandId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CanExecuteCommand(commandId) || _viewModel is null)
        {
            return false;
        }

        var command = commandId switch
        {
            "previous" => _viewModel.PreviousCommand,
            "play-pause" => _viewModel.PlayPauseCommand,
            "next" => _viewModel.NextCommand,
            _ => null
        };
        if (command is null)
        {
            return false;
        }

        await command.ExecuteAsync(null);
        return true;
    }

    public ValueTask ActivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LifecycleState = ModuleLifecycleState.Active;
        PresentationChanged?.Invoke(this, CurrentPresentation);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LifecycleState = ModuleLifecycleState.Inactive;
        PresentationChanged?.Invoke(this, null);
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        if (_viewModel is null)
        {
            if (_localization is not null)
            {
                _localization.LanguageChanged -= OnLanguageChanged;
            }
            return;
        }

        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MusicModuleViewModel.Current))
        {
            PresentationChanged?.Invoke(this, CurrentPresentation);
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs args) =>
        PresentationChanged?.Invoke(this, CurrentPresentation);

    private string Text(string key, string fallback) =>
        _localization?.Get(key) is { } value && value != key ? value : fallback;

}
