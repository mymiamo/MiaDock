using MiaDock.Modules.DeviceStatus.Models;

namespace MiaDock.Modules.DeviceStatus.Services;

public interface IBluetoothDeviceConnectionService
{
    Task<BluetoothConnectionResult> ConnectAsync(
        BluetoothConnectionRequest request,
        CancellationToken cancellationToken = default);

    Task<BluetoothConnectionResult> DisconnectAsync(
        BluetoothConnectionRequest request,
        CancellationToken cancellationToken = default);
}
