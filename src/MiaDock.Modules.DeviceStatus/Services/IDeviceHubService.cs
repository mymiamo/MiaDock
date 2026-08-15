using MiaDock.Modules.DeviceStatus.Models;

namespace MiaDock.Modules.DeviceStatus.Services;

public interface IDeviceHubService : IAsyncDisposable
{
    DeviceHubState Current { get; }

    event EventHandler<DeviceHubState>? StateChanged;

    event EventHandler<DeviceHubChange>? DeviceChanged;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);

    Task RefreshAsync(CancellationToken cancellationToken = default);

    void NotifySafeToRemove(DeviceHubDevice device);
}
