using MiaDock.Core.Modules;

namespace MiaDock.Modules.Media;

public sealed class MusicModule : IIslandModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "media",
        "Music",
        100,
        "MusicCompactView",
        "MusicExpandedView",
        new HashSet<ModuleEventKind>
        {
            ModuleEventKind.TrackChanged,
            ModuleEventKind.PlaybackChanged,
            ModuleEventKind.TimelineChanged
        },
        TimeSpan.FromSeconds(5));

    public ModuleLifecycleState LifecycleState { get; private set; }

    public bool IsEnabled { get; set; } = true;

    public ValueTask ActivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LifecycleState = ModuleLifecycleState.Active;
        return ValueTask.CompletedTask;
    }

    public ValueTask DeactivateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LifecycleState = ModuleLifecycleState.Inactive;
        return ValueTask.CompletedTask;
    }
}
