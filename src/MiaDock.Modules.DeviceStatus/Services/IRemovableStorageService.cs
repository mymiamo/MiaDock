using MiaDock.Modules.DeviceStatus.Models;

namespace MiaDock.Modules.DeviceStatus.Services;

public interface IRemovableStorageService
{
    Task<IReadOnlyList<RemovableStorageInfo>> GetDevicesAsync(CancellationToken cancellationToken = default);

    Task<bool> OpenAsync(RemovableStorageInfo storage, CancellationToken cancellationToken = default);

    Task<RemovableStorageEjectResult> EjectAsync(
        RemovableStorageInfo storage,
        CancellationToken cancellationToken = default);

    Task<bool> OpenSafelyRemoveHardwareAsync(CancellationToken cancellationToken = default);
}
