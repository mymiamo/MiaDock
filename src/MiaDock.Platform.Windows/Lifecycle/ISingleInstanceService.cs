namespace MiaDock.Platform.Windows.Lifecycle;

public interface ISingleInstanceService : IDisposable
{
    bool IsCurrentInstance { get; }

    event EventHandler? ActivationRedirected;

    Task<bool> RegisterOrRedirectAsync(
        string instanceKey,
        CancellationToken cancellationToken = default);
}
