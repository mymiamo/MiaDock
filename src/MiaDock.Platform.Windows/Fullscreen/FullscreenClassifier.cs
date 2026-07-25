namespace MiaDock.Platform.Windows.Fullscreen;

public static class FullscreenClassifier
{
    private const int RunningDirect3DFullscreen = 3;
    private const int BoundsTolerance = 2;

    public static FullscreenDetectionReason Classify(FullscreenEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!input.IsVisible || input.IsMinimized || input.IsCloaked || input.IsOwnProcess || input.IsShellWindow)
        {
            return FullscreenDetectionReason.None;
        }

        if (input.UserNotificationState == RunningDirect3DFullscreen)
        {
            return FullscreenDetectionReason.ExclusiveDirect3D;
        }

        if (input.IsStandardMaximizedWindow)
        {
            return FullscreenDetectionReason.None;
        }

        var window = input.WindowBounds;
        var monitor = input.MonitorBounds;
        var coversMonitor = window.Left <= monitor.Left + BoundsTolerance
            && window.Top <= monitor.Top + BoundsTolerance
            && window.Right >= monitor.Right - BoundsTolerance
            && window.Bottom >= monitor.Bottom - BoundsTolerance;
        return coversMonitor
            ? FullscreenDetectionReason.WindowCoversMonitor
            : FullscreenDetectionReason.None;
    }
}
