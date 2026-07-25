namespace MiaDock.Core.Modules;

public sealed record ModuleDisplayState(
    ModuleDescriptor Descriptor,
    ModulePresentation Presentation,
    ModuleEvent? Event = null)
{
    public bool IsNotification => Event is not null;

    public bool KeepsManualSelection =>
        Presentation.IsPersistentOverride ?? Descriptor.IsPersistent;
}
