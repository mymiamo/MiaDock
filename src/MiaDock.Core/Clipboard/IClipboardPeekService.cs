namespace MiaDock.Core.Clipboard;

public interface IClipboardPeekService : IAsyncDisposable
{
    ClipboardPeekState Current { get; }

    event EventHandler<ClipboardPeekState>? StateChanged;

    event EventHandler<ClipboardPeekItem>? ItemCaptured;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task<ClipboardPeekActionResult> ClearHistoryAsync(CancellationToken cancellationToken = default);

    Task<ClipboardPeekActionResult> CopyAsync(ClipboardPeekItem item, CancellationToken cancellationToken = default);

    Task<ClipboardPeekActionResult> CopyTextAsync(string text, CancellationToken cancellationToken = default);

    Task<ClipboardPeekActionResult> OpenAsync(ClipboardPeekItem item, CancellationToken cancellationToken = default);

    Task<ClipboardPeekActionResult> OpenContainingFolderAsync(ClipboardPeekItem item, CancellationToken cancellationToken = default);

    Task<ClipboardPeekActionResult> SaveImageAsync(ClipboardPeekItem item, nint ownerWindow, CancellationToken cancellationToken = default);

    Task<ClipboardPeekRevealResult> RevealSensitiveAsync(string itemId, CancellationToken cancellationToken = default);
}
