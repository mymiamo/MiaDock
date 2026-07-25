namespace MiaDock.Platform.Windows.Startup;

public interface IStartupTaskService
{
    Task<StartupTaskStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<StartupTaskStatus> SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default);
}
