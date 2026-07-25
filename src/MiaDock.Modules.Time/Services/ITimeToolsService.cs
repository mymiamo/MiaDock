using MiaDock.Modules.Time.Models;

namespace MiaDock.Modules.Time.Services;

public interface ITimeToolsService : IAsyncDisposable
{
    TimeToolsSnapshot Current { get; }

    event EventHandler<TimeToolsSnapshot>? SnapshotChanged;

    Task InitializeAsync(CancellationToken cancellationToken = default);

    bool StartTimer(TimeSpan duration);
    bool PauseTimer();
    bool ResumeTimer();
    bool CancelTimer();
    bool ConsumePendingCompletion();

    bool StartStopwatch();
    bool PauseStopwatch();
    bool AddLap();
    bool ResetStopwatch();
}
