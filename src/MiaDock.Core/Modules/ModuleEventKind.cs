namespace MiaDock.Core.Modules;

public enum ModuleEventKind
{
    TrackChanged,
    PlaybackChanged,
    TimelineChanged,
    StatusChanged,
    ValueChanged,
    Started,
    ProgressChanged,
    Completed,
    Warning,
    Critical,
    Notification
}
