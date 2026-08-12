namespace MiaDock.Core.Lifecycle;

public interface ICrashStateStore
{
    void MarkSessionStarted();

    void MarkCleanShutdown();

    void MarkCrashed(Exception? exception);

    bool TryBeginRestart();

    bool TryConsumePendingCrash(out CrashStateRecord record);
}
