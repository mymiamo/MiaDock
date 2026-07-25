namespace MiaDock.Core.Modules;

public interface IModuleOrchestrator : IDisposable
{
    IReadOnlyList<ModuleDisplayState> AvailableModules { get; }

    ModuleDisplayState? CurrentDisplay { get; }

    ModuleEvent? ActiveEvent { get; }

    int PendingEventCount { get; }

    TimeSpan TemporarySelectionDuration => ModuleOrchestrator.DefaultTemporarySelectionDuration;

    DateTimeOffset? TemporarySelectionExpiresAtUtc => null;

    event EventHandler<ModuleDisplayState?>? CurrentDisplayChanged;

    event EventHandler<ModuleEvent?>? ActiveEventChanged;

    bool SelectModule(string moduleId);

    bool SelectDefault();

    bool MoveSelection(int offset);

    void EndManualSelection();

    bool NotifyUserInteraction() => false;

    void UpdateTemporarySelectionDuration(TimeSpan duration)
    {
    }

    bool ExpireTemporarySelection() => false;

    bool CompleteActiveEvent();
}
