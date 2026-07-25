namespace MiaDock.Modules.Time.Services;

public interface ISystemResumeService : IDisposable
{
    event EventHandler? Resumed;

    void Start();
}
