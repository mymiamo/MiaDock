namespace MiaDock.Core.Clipboard;

public sealed record ClipboardPeekOptions(
    int HistoryLimit,
    ClipboardPeekEventMode EventMode,
    bool ShowImageEvents)
{
    public static ClipboardPeekOptions Default { get; } = new(5, ClipboardPeekEventMode.SmartOnly, true);
}

public interface IClipboardPeekSettings
{
    ClipboardPeekOptions Current { get; }
    TimeSpan EventDuration { get; }
    event EventHandler<ClipboardPeekOptions>? Changed;
}
