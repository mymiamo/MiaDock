namespace MiaDock.Platform.Windows.Fullscreen;

public sealed record FullscreenSnapshot(
    bool IsFullscreen,
    nint WindowHandle,
    nint MonitorHandle,
    FullscreenDetectionReason Reason)
{
    public static FullscreenSnapshot None { get; } = new(false, 0, 0, FullscreenDetectionReason.None);
}
