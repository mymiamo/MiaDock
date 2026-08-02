using MiaDock.Core.Logging;
using MiaDock.Platform.Windows.Startup;

namespace MiaDock.App.Services;

public sealed class StartupTaskCoordinator
{
    private readonly IStartupTaskService _startupTask;
    private readonly ISettingsService _settings;
    private readonly ILogService _log;

    public StartupTaskCoordinator(
        IStartupTaskService startupTask,
        ISettingsService settings,
        ILogService log)
    {
        _startupTask = startupTask;
        _settings = settings;
        _log = log;
    }

    public async Task<StartupTaskStatus> ReconcileAsync(
        CancellationToken cancellationToken = default)
    {
        var desired = _settings.Current.StartupShutdown.StartWithWindows;
        var status = await _startupTask.GetStatusAsync(cancellationToken);
        if (desired && status == StartupTaskStatus.Disabled)
        {
            status = await _startupTask.SetEnabledAsync(
                enabled: true,
                cancellationToken: cancellationToken);
        }
        else if (!desired && status == StartupTaskStatus.Enabled)
        {
            status = await _startupTask.SetEnabledAsync(
                enabled: false,
                cancellationToken: cancellationToken);
        }

        var isEnabled = status is
            StartupTaskStatus.Enabled or
            StartupTaskStatus.EnabledByPolicy;
        if (status is not
            (StartupTaskStatus.Unavailable or StartupTaskStatus.Failed) &&
            _settings.Current.StartupShutdown.StartWithWindows != isEnabled)
        {
            _settings.Update(settings => settings with
            {
                StartupShutdown = settings.StartupShutdown with
                {
                    StartWithWindows = isEnabled
                }
            });
        }

        _log.Write(
            status is StartupTaskStatus.Failed
                ? TechnicalLogLevel.Warning
                : TechnicalLogLevel.Information,
            status is StartupTaskStatus.Failed
                ? TechnicalEventIds.StartupTaskRepairFailed
                : TechnicalEventIds.StartupTaskChecked,
            "Startup",
            "Windows startup task state was reconciled.",
            properties: new Dictionary<string, object?>
            {
                ["desired"] = desired,
                ["status"] = status.ToString()
            });
        return status;
    }
}
