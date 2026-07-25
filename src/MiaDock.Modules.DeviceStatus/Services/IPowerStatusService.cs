using MiaDock.Modules.DeviceStatus.Models;

namespace MiaDock.Modules.DeviceStatus.Services;

public interface IPowerStatusService : IDisposable
{
    BatteryStatusSnapshot Current { get; }
    event EventHandler<BatteryStatusSnapshot>? SnapshotChanged;
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
