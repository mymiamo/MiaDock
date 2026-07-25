namespace MiaDock.Core.Modules;

public interface IIslandModule
{
    ModuleDescriptor Descriptor { get; }

    ModuleLifecycleState LifecycleState { get; }

    bool IsEnabled { get; set; }

    ModulePresentation? CurrentPresentation { get; }

    event EventHandler<ModulePresentation?>? PresentationChanged;

    event EventHandler<ModuleEvent>? EventOccurred;

    bool CanExecuteCommand(string commandId);

    ValueTask<bool> ExecuteCommandAsync(
        string commandId,
        CancellationToken cancellationToken = default);

    ValueTask ActivateAsync(CancellationToken cancellationToken = default);

    ValueTask DeactivateAsync(CancellationToken cancellationToken = default);
}
