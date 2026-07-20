namespace MiaDock.Core.Modules;

public interface IIslandModule
{
    ModuleDescriptor Descriptor { get; }

    ModuleLifecycleState LifecycleState { get; }

    bool IsEnabled { get; set; }

    ValueTask ActivateAsync(CancellationToken cancellationToken = default);

    ValueTask DeactivateAsync(CancellationToken cancellationToken = default);
}
