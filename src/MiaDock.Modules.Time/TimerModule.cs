using System.ComponentModel;
using MiaDock.Core.Modules;
using MiaDock.Modules.Time.Models;
using MiaDock.Modules.Time.Services;
using MiaDock.Modules.Time.ViewModels;

namespace MiaDock.Modules.Time;

public sealed class TimerModule : IIslandModule, IDisposable
{
    private readonly TimeToolsViewModel _viewModel;
    private readonly ITimeToolsService _service;
    private TimerRunState _previousState;
    private bool _isEnabled = true;

    public TimerModule(TimeToolsViewModel viewModel, ITimeToolsService service)
    {
        _viewModel = viewModel;
        _service = service;
        _previousState = viewModel.Current.TimerState;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public ModuleDescriptor Descriptor { get; } = new(
        "timer",
        "Zaman",
        380,
        "TimerCompactView",
        "TimerExpandedView",
        new HashSet<ModuleEventKind>
        {
            ModuleEventKind.Started,
            ModuleEventKind.ProgressChanged,
            ModuleEventKind.Completed
        },
        TimeSpan.FromSeconds(5),
        [
            new ModuleCommandDescriptor("pause-resume", "Duraklat veya devam et", "\uE768"),
            new ModuleCommandDescriptor("cancel", "Zamanlayıcıyı iptal et", "\uE711")
        ],
        "TimerNotificationView",
        500,
        isPersistent: false,
        hoverViewKey: "TimerHoverView",
        iconGlyph: "\uE823");

    public ModuleLifecycleState LifecycleState { get; private set; }
    public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; PresentationChanged?.Invoke(this, CurrentPresentation); } }

    public ModulePresentation? CurrentPresentation
    {
        get
        {
            var current = _viewModel.Current;
            var timerActive = current.TimerState is TimerRunState.Running or TimerRunState.Paused;
            var stopwatchActive = current.IsStopwatchRunning ||
                                  current.StopwatchElapsed > TimeSpan.Zero ||
                                  current.Laps.Count > 0;
            var hasActiveWork = timerActive || stopwatchActive;
            return new ModulePresentation(
                Descriptor.Id,
                hasActiveWork ? _viewModel.CompactTimeText : "Zaman araçları",
                hasActiveWork ? _viewModel.CompactStatusText : "Zamanlayıcı ve kronometre",
                "\uE823",
                hasActiveWork ? ModuleIndicatorKind.Value : ModuleIndicatorKind.None,
                valueText: hasActiveWork ? _viewModel.CompactTimeText : null,
                progress: timerActive ? current.TimerProgress : null,
                presentationKind: hasActiveWork ? ModulePresentationKind.Progress : ModulePresentationKind.Standard,
                commands: Descriptor.InteractionCommands.Select(command => new ModuleCommandState(
                    command.Id, command.DisplayName, command.Glyph, CanExecuteCommand(command.Id))).ToArray(),
                isPersistentOverride: hasActiveWork,
                persistentPriorityOverride: hasActiveWork ? 500 : null);
        }
    }

    public event EventHandler<ModulePresentation?>? PresentationChanged;
    public event EventHandler<ModuleEvent>? EventOccurred;

    public bool CanExecuteCommand(string commandId) => commandId switch
    {
        "pause-resume" => _viewModel.CompactPrimaryCommand.CanExecute(null),
        "cancel" => _viewModel.CompactSecondaryCommand.CanExecute(null),
        _ => false
    };

    public ValueTask<bool> ExecuteCommandAsync(string commandId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var command = commandId switch
        {
            "pause-resume" => _viewModel.CompactPrimaryCommand,
            "cancel" => _viewModel.CompactSecondaryCommand,
            _ => null
        };
        if (command?.CanExecute(null) != true)
        {
            return ValueTask.FromResult(false);
        }

        command.Execute(null);
        return ValueTask.FromResult(true);
    }

    public ValueTask ActivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LifecycleState = ModuleLifecycleState.Active;
        PresentationChanged?.Invoke(this, CurrentPresentation);
        if (_viewModel.Current.TimerState == TimerRunState.Completed && _service.ConsumePendingCompletion())
        {
            PublishCompleted();
        }
        return ValueTask.CompletedTask;
    }

    public ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LifecycleState = ModuleLifecycleState.Inactive;
        PresentationChanged?.Invoke(this, null);
        return ValueTask.CompletedTask;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(TimeToolsViewModel.Current)) return;
        PresentationChanged?.Invoke(this, CurrentPresentation);
        var state = _viewModel.Current.TimerState;
        if (state == TimerRunState.Completed && _previousState != TimerRunState.Completed)
        {
            _service.ConsumePendingCompletion();
            PublishCompleted();
        }
        _previousState = state;
    }

    private void PublishCompleted()
    {
        var presentation = new ModulePresentation(
            Descriptor.Id,
            "Süre doldu",
            "Zamanlayıcı tamamlandı",
            "\uE823",
            ModuleIndicatorKind.StatusDot,
            presentationKind: ModulePresentationKind.Alert);
        EventOccurred?.Invoke(this, new ModuleEvent(
            Descriptor.Id,
            ModuleEventKind.Completed,
            presentation,
            Descriptor.DefaultDisplayDuration,
            DateTimeOffset.UtcNow,
            ModuleEventPriority.Critical,
            "timer:completed",
            isFullscreenEligible: true));
    }

    public void Dispose() => _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
}
