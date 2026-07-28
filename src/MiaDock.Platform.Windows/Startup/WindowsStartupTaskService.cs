using Windows.ApplicationModel;
using System.Runtime.InteropServices;

namespace MiaDock.Platform.Windows.Startup;

public sealed class WindowsStartupTaskService : IStartupTaskService, IDisposable
{
    public const string TaskId = "MiaDockStartupTask";
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public async Task<StartupTaskStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var task = await TryGetTaskAsync();
            return task is null ? StartupTaskStatus.Unavailable : Map(task.State);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<StartupTaskStatus> SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            var task = await TryGetTaskAsync();
            if (task is null)
            {
                return StartupTaskStatus.Unavailable;
            }

            try
            {
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
            }
            catch (Exception exception) when (IsRecoverableWindowsApiException(exception))
            {
                return StartupTaskStatus.Failed;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Map(task.State);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private static async Task<StartupTask?> TryGetTaskAsync()
    {
        try
        {
            _ = Package.Current.Id.Name;
            return await StartupTask.GetAsync(TaskId);
        }
        catch (Exception exception) when (IsRecoverableWindowsApiException(exception))
        {
            return null;
        }
    }

    private static bool IsRecoverableWindowsApiException(Exception exception) =>
        exception is InvalidOperationException
            or ArgumentException
            or UnauthorizedAccessException
            or COMException;

    private static StartupTaskStatus Map(StartupTaskState state) => state switch
    {
        StartupTaskState.Disabled => StartupTaskStatus.Disabled,
        StartupTaskState.DisabledByUser => StartupTaskStatus.DisabledByUser,
        StartupTaskState.DisabledByPolicy => StartupTaskStatus.DisabledByPolicy,
        StartupTaskState.Enabled => StartupTaskStatus.Enabled,
        StartupTaskState.EnabledByPolicy => StartupTaskStatus.EnabledByPolicy,
        _ => StartupTaskStatus.Unavailable
    };

    public void Dispose() => _operationGate.Dispose();
}
