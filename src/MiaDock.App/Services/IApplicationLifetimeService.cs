namespace MiaDock.App.Services;

public interface IApplicationLifetimeService
{
    bool IsShuttingDown { get; }

    event EventHandler? ExitRequested;

    void RequestExit();
}
