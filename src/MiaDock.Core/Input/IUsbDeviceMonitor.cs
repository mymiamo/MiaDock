namespace MiaDock.Core.Input;

public interface IUsbDeviceMonitor : IAsyncDisposable
{
    event EventHandler<UsbDeviceChangedEventArgs>? DeviceChanged;

    bool IsRunning { get; }

    ValueTask StartAsync(CancellationToken cancellationToken = default);

    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
