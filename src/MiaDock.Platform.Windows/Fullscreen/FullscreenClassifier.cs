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

        var window = input.WindowBounds;
        var monitor = input.MonitorBounds;
        var coversMonitor = window.Left <= monitor.Left + BoundsTolerance
            && window.Top <= monitor.Top + BoundsTolerance
            && window.Right >= monitor.Right - BoundsTolerance
            && window.Bottom >= monitor.Bottom - BoundsTolerance;
        // Chromium, Firefox and native video players can retain their normal
        // maximized HWND style while their client area switches to true video
        // fullscreen. Their client bounds cover the entire monitor, whereas a
        // normal maximized window stops at the work area. Test coverage must
        // therefore run before the standard-maximized safeguard.
        return coversMonitor
            ? FullscreenDetectionReason.WindowCoversMonitor
            : FullscreenDetectionReason.None;
    }
}
