using Microsoft.Windows.System.Power;
using MiaDock.Modules.Time.Services;

namespace MiaDock.Platform.Windows.Power;

public sealed class WindowsSystemResumeService : ISystemResumeService
{
    private bool _started;

    public event EventHandler? Resumed;

    public void Start()
    {
        if (_started) return;
        PowerManager.SystemSuspendStatusChanged += OnSystemSuspendStatusChanged;
        _started = true;
    }

    private void OnSystemSuspendStatusChanged(object? sender, object args)
    {
        if (PowerManager.SystemSuspendStatus is SystemSuspendStatus.AutoResume or SystemSuspendStatus.ManualResume)
        {
            Resumed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (!_started) return;
        PowerManager.SystemSuspendStatusChanged -= OnSystemSuspendStatusChanged;
        _started = false;
    }
}
