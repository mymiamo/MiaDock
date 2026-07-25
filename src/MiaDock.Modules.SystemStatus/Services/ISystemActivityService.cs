using MiaDock.Modules.SystemStatus.Models;

namespace MiaDock.Modules.SystemStatus.Services;

public interface ISystemActivityService : IAsyncDisposable
{
    SystemActivitySnapshot Current { get; }

    event EventHandler<SystemActivitySnapshot>? SnapshotChanged;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task<bool> SetMasterVolumeAsync(double volume, CancellationToken cancellationToken = default);

    Task<bool> ToggleMasterMuteAsync(CancellationToken cancellationToken = default);

    Task<bool> SetApplicationVolumeAsync(double volume, CancellationToken cancellationToken = default);

    Task<bool> ToggleApplicationMuteAsync(CancellationToken cancellationToken = default);
}
