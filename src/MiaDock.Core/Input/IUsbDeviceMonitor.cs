namespace MiaDock.Core.Input;

public interface IUsbDeviceMonitor : IAsyncDisposable
{
    event EventHandler<UsbDeviceChangedEventArgs>? DeviceChanged;

    bool IsRunning { get; }

    ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default);
}
