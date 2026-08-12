using Microsoft.Windows.AppLifecycle;

namespace MiaDock.Platform.Windows.Lifecycle;

public sealed class WindowsSingleInstanceService : ISingleInstanceService
{
    private AppInstance? _registeredInstance;
    private bool _disposed;

    public bool IsCurrentInstance { get; private set; }

    public event EventHandler? ActivationRedirected;

    public async Task<bool> RegisterOrRedirectAsync(
        string instanceKey,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceKey);
        cancellationToken.ThrowIfCancellationRequested();

        var current = AppInstance.GetCurrent();
        var activation = current.GetActivatedEventArgs();
        var registered = AppInstance.FindOrRegisterForKey(instanceKey);
        if (!registered.IsCurrent)
        {
            await registered.RedirectActivationToAsync(activation);
            return false;
        }

        _registeredInstance = registered;
        _registeredInstance.Activated += OnActivated;
        IsCurrentInstance = true;
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_registeredInstance is not null)
        {
            _registeredInstance.Activated -= OnActivated;
            try
            {
                _registeredInstance.UnregisterKey();
            }
            catch
            {
                // Windows also removes the registration when the process exits.
            }
            _registeredInstance = null;
        }

        IsCurrentInstance = false;
        _disposed = true;
    }

    private void OnActivated(object? sender, AppActivationArguments args) =>
        ActivationRedirected?.Invoke(this, EventArgs.Empty);
}
