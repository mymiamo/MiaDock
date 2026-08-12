namespace MiaDock.Platform.Windows.Fullscreen;

internal sealed class ExclusiveFullscreenSignalTracker
{
    private const int RunningDirect3DFullscreen = 3;
    private nint _ownerWindow;

    public int Filter(nint foregroundWindow, int notificationState)
    {
        if (notificationState != RunningDirect3DFullscreen)
        {
            _ownerWindow = 0;
            return notificationState;
        }

        if (_ownerWindow == 0)
        {
            _ownerWindow = foregroundWindow;
        }

        return foregroundWindow == _ownerWindow
            ? notificationState
            : 0;
    }
}
