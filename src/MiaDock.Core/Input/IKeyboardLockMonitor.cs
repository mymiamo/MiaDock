namespace MiaDock.Core.Input;

public interface IKeyboardLockMonitor : IAsyncDisposable
{
    event EventHandler<KeyboardLockStateChangedEventArgs>? StateChanged;

    bool IsRunning { get; }

    ValueTask StartAsync(CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
