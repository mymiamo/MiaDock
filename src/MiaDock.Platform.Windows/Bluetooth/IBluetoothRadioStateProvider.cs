using MiaDock.Modules.DeviceStatus.Models;

namespace MiaDock.Platform.Windows.Bluetooth;

public interface IBluetoothRadioStateProvider : IDisposable
{
    BluetoothRadioState Current { get; }

    event EventHandler<BluetoothRadioState>? StateChanged;

    Task StartAsync(CancellationToken cancellationToken = default);

    void Stop();
}
