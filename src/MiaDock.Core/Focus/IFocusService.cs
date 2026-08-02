namespace MiaDock.Core.Focus;

public interface IFocusService : IDisposable
{
    FocusSnapshot Current { get; }

    event EventHandler<FocusChangedEventArgs>? FocusChanged;

    void Start();

    bool Activate(
        string profileId,
        FocusActivationSource source = FocusActivationSource.Manual);

    bool ActivateFor(
        string profileId,
        TimeSpan duration,
        FocusActivationSource source = FocusActivationSource.Manual);

    bool ActivateIndefinitely(
        string profileId,
        FocusActivationSource source = FocusActivationSource.Manual);

    bool Deactivate();

    bool Refresh();
}
