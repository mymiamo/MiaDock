namespace MiaDock.Core.Modules;

public interface IIslandModuleRegistry : IAsyncDisposable
{
    IReadOnlyList<IIslandModule> Modules { get; }

    IIslandModule? ActiveModule { get; }

    ModulePresentation? ActivePresentation { get; }

    event EventHandler<ModulePresentation?>? ActivePresentationChanged;

    event EventHandler<ModuleEvent>? ModuleEventOccurred;

    bool CanExecuteCommand(string moduleId, string commandId);

    ValueTask<bool> ExecuteCommandAsync(
        string moduleId,
        string commandId,
        CancellationToken cancellationToken = default);

    ValueTask<bool> SetEnabledAsync(
        string moduleId,
        bool enabled,
        CancellationToken cancellationToken = default);

    ValueTask InitializeAsync(CancellationToken cancellationToken = default);
}
