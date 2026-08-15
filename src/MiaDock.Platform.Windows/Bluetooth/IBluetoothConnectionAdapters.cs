using MiaDock.Modules.DeviceStatus.Models;

namespace MiaDock.Platform.Windows.Bluetooth;

internal enum BluetoothProfileOperationResult
{
    Succeeded,
    Unavailable,
    AccessDenied,
    NoMatchingService,
    Failed
}

internal interface IBluetoothProfileController
{
    BluetoothProfileOperationResult SetServices(ulong address, IReadOnlyList<Guid> services, bool enable);
}

internal interface IBluetoothAclConnector
{
    Task<BluetoothConnectionResult> ConnectAsync(string endpointId, CancellationToken cancellationToken);
}
