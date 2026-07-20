namespace MiaDock.Core.Modules;

public sealed record ModuleDescriptor
{
    public ModuleDescriptor(
        string id,
        string displayName,
        int priority,
        string compactViewKey,
        string expandedViewKey,
        IReadOnlySet<ModuleEventKind> supportedEvents,
        TimeSpan defaultDisplayDuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(compactViewKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(expandedViewKey);
        ArgumentNullException.ThrowIfNull(supportedEvents);

        if (priority < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(priority));
        }

        if (defaultDisplayDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultDisplayDuration));
        }

        Id = id;
        DisplayName = displayName;
        Priority = priority;
        CompactViewKey = compactViewKey;
        ExpandedViewKey = expandedViewKey;
        SupportedEvents = supportedEvents;
        DefaultDisplayDuration = defaultDisplayDuration;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public int Priority { get; }

    public string CompactViewKey { get; }

    public string ExpandedViewKey { get; }

    public IReadOnlySet<ModuleEventKind> SupportedEvents { get; }

    public TimeSpan DefaultDisplayDuration { get; }
}
