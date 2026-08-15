using MiaDock.Modules.DeviceStatus.Models;

namespace MiaDock.Modules.DeviceStatus.Services;

public interface IBluetoothStatusService : IDisposable
{
    BluetoothStatusSnapshot Current { get; }
    event EventHandler<BluetoothStatusSnapshot>? SnapshotChanged;
    ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default);
}
