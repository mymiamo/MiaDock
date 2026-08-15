namespace MiaDock.Core.Clipboard;

public enum ClipboardPeekContentType
{
    PlainText,
    Url,
    Email,
    Color,
    File,
    Folder,
    Image,
    Unknown,
    Sensitive
}

[Flags]
public enum ClipboardPeekCapabilities
{
    None = 0,
    Copy = 1,
    Open = 2,
    OpenFolder = 4,
    ComposeEmail = 8,
    SaveImage = 16,
    Reveal = 32
}

public sealed record ClipboardImagePreview(
    int Width,
    int Height,
    string? Format,
    byte[]? ThumbnailPng);

public sealed record ClipboardPeekItem(
    string Id,
    ClipboardPeekContentType Type,
    string DisplayText,
    string? RawText,
    DateTimeOffset CreatedAt,
    string SourceFormat,
    bool IsSensitive,
    ClipboardPeekCapabilities AvailableActions,
    Uri? Uri = null,
    string? EmailAddress = null,
    string? ColorValue = null,
    string? FilePath = null,
    ClipboardImagePreview? Image = null,
    int? ItemCount = null)
{
    public bool IsRevealable => AvailableActions.HasFlag(ClipboardPeekCapabilities.Reveal);
}

public sealed record ClipboardPeekState(
    ClipboardPeekItem? CurrentItem,
    IReadOnlyList<ClipboardPeekItem> History,
    bool IsInitialSnapshot)
{
    public static ClipboardPeekState Empty { get; } = new(null, Array.Empty<ClipboardPeekItem>(), true);
}

public enum ClipboardPeekEventMode
{
    SmartOnly,
    Everything,
    Never
}

public enum ClipboardPeekActionResult
{
    Succeeded,
    Cancelled,
    Unavailable,
    AccessDenied,
    Unsupported,
    Failed
}

public sealed record ClipboardPeekRevealResult(
    ClipboardPeekActionResult Result,
    string? Value)
{
    public static ClipboardPeekRevealResult Unavailable { get; } =
        new(ClipboardPeekActionResult.Unavailable, null);
}
