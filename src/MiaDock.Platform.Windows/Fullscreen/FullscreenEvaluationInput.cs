namespace MiaDock.Platform.Windows.Fullscreen;

public readonly record struct PixelBounds(int Left, int Top, int Right, int Bottom);

public sealed record FullscreenEvaluationInput(
    bool IsVisible,
    bool IsMinimized,
    bool IsCloaked,
    bool IsOwnProcess,
    bool IsShellWindow,
    bool IsStandardMaximizedWindow,
    PixelBounds WindowBounds,
    PixelBounds MonitorBounds,
    int UserNotificationState);
