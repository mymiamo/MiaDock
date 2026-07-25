namespace MiaDock.App.Services;

public sealed class ApplicationLifetimeService : IApplicationLifetimeService
{
    public bool IsShuttingDown { get; private set; }

    public event EventHandler? ExitRequested;

    public void RequestExit()
    {
        if (IsShuttingDown)
        {
            return;
        }

        IsShuttingDown = true;
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }
}
