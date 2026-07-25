using Microsoft.UI.Dispatching;
using MiaDock.Core.Threading;

namespace MiaDock.App.Infrastructure;

public sealed class DispatcherQueueUiDispatcher : IUiDispatcher
{
    private readonly DispatcherQueue _dispatcherQueue;

    public DispatcherQueueUiDispatcher()
    {
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread()
            ?? throw new InvalidOperationException("A UI DispatcherQueue is required.");
    }

    public bool HasThreadAccess => _dispatcherQueue.HasThreadAccess;

    public bool TryEnqueue(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return _dispatcherQueue.TryEnqueue(() => callback());
    }
}
