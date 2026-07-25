using MiaDock.Modules.Time.Persistence;

namespace MiaDock.Modules.Time.Services;

public interface ITimerStateStore
{
    Task<TimerPersistentState?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(TimerPersistentState state, CancellationToken cancellationToken = default);
}
