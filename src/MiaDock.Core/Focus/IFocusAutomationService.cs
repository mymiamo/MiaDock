namespace MiaDock.Core.Focus;

public interface IFocusAutomationService : IDisposable
{
    bool IsStarted { get; }

    void Start();

    void Refresh();
}
