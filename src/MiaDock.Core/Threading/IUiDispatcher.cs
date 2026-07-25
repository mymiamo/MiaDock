namespace MiaDock.Core.Threading;

public interface IUiDispatcher
{
    bool HasThreadAccess { get; }

    bool TryEnqueue(Action callback);
}
