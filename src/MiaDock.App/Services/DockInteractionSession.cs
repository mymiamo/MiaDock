namespace MiaDock.App.Services;

public static class DockInteractionSession
{
    private static readonly object Gate = new();
    private static readonly HashSet<object> Owners =
        new(ReferenceEqualityComparer.Instance);

    public static event EventHandler<bool>? ActivityChanged;

    public static bool IsActive
    {
        get
        {
            lock (Gate)
            {
                return Owners.Count > 0;
            }
        }
    }

    public static void Begin(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var changed = false;
        lock (Gate)
        {
            changed = Owners.Add(owner) && Owners.Count == 1;
        }

        if (changed)
        {
            ActivityChanged?.Invoke(null, true);
        }
    }

    public static IDisposable Enter(object owner)
    {
        Begin(owner);
        return new InteractionToken(owner);
    }

    public static void End(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var changed = false;
        lock (Gate)
        {
            changed = Owners.Remove(owner) && Owners.Count == 0;
        }

        if (changed)
        {
            ActivityChanged?.Invoke(null, false);
        }
    }

    private sealed class InteractionToken(object owner) : IDisposable
    {
        private object? _owner = owner;

        public void Dispose()
        {
            var current = Interlocked.Exchange(ref _owner, null);
            if (current is not null)
            {
                End(current);
            }
        }
    }
}
