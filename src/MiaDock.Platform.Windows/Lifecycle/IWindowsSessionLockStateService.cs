namespace MiaDock.Platform.Windows.Lifecycle;

public interface IWindowsSessionLockStateService : IDisposable
{
    bool IsLocked { get; }

    event EventHandler<bool>? LockStateChanged;

    void Start();
}
