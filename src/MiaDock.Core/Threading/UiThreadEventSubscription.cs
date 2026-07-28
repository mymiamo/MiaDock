namespace MiaDock.Core.Threading;

public sealed class UiThreadEventSubscription(
    IUiDispatcher dispatcher,
    Action unsubscribe) : IAsyncDisposable
{
    private Action? _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));

    public ValueTask DisposeAsync()
    {
        var callback = Interlocked.Exchange(ref _unsubscribe, null);
        if (callback is null)
        {
            return ValueTask.CompletedTask;
        }

        if (dispatcher.HasThreadAccess)
        {
            InvokeSafely(callback);
            return ValueTask.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcher.TryEnqueue(() =>
            {
                InvokeSafely(callback);
                completion.TrySetResult();
            }))
        {
            // The UI queue is already shutting down. Abandoning the native
            // subscription is safer than invoking its remover on the wrong thread.
            completion.TrySetResult();
        }

        return new ValueTask(completion.Task);
    }

    private static void InvokeSafely(Action callback)
    {
        try
        {
            callback();
        }
        catch
        {
            // Event removal is best effort during application shutdown.
        }
    }
}
