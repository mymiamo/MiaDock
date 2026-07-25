using Windows.ApplicationModel;
using System.Runtime.InteropServices;

namespace MiaDock.Platform.Windows.Startup;

public sealed class WindowsStartupTaskService : IStartupTaskService
{
    public const string TaskId = "MiaDockStartupTask";

    public async Task<StartupTaskStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var task = await TryGetTaskAsync();
        return task is null ? StartupTaskStatus.Unavailable : Map(task.State);
    }

    public async Task<StartupTaskStatus> SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var task = await TryGetTaskAsync();
        if (task is null)
        {
            return StartupTaskStatus.Unavailable;
        }

        if (enabled)
        {
            if (task.State == StartupTaskState.Disabled)
            {
                _ = await task.RequestEnableAsync();
            }
        }
        else if (task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy)
        {
            task.Disable();
        }

        cancellationToken.ThrowIfCancellationRequested();
        return Map(task.State);
    }

    private static async Task<StartupTask?> TryGetTaskAsync()
    {
        try
        {
            _ = Package.Current.Id.Name;
            return await StartupTask.GetAsync(TaskId);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or UnauthorizedAccessException or COMException)
        {
            return null;
        }
    }

    private static StartupTaskStatus Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Disabled => StartupTaskStatus.Disabled,
        StartupTaskState.DisabledByUser => StartupTaskStatus.DisabledByUser,
        StartupTaskState.DisabledByPolicy => StartupTaskStatus.DisabledByPolicy,
        StartupTaskState.Enabled => StartupTaskStatus.Enabled,
        StartupTaskState.EnabledByPolicy => StartupTaskStatus.EnabledByPolicy,
        _ => StartupTaskStatus.Unavailable
    };
}
