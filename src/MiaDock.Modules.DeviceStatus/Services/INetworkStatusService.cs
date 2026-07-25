using MiaDock.Modules.DeviceStatus.Models;

namespace MiaDock.Modules.DeviceStatus.Services;

public interface INetworkStatusService : IDisposable
{
    NetworkStatusSnapshot Current { get; }
    event EventHandler<NetworkStatusSnapshot>? SnapshotChanged;
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    ValueTask StopAsync(CancellationToken cancellationToken = default);
    void SetThroughputSamplingEnabled(bool enabled);
}
