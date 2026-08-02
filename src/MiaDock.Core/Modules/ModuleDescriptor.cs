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
        TimeSpan defaultDisplayDuration,
        IReadOnlyList<ModuleCommandDescriptor>? interactionCommands = null,
        string? notificationViewKey = null,
        int? persistentPriority = null,
        bool isPersistent = true,
        string? hoverViewKey = null,
        string? iconGlyph = null,
        string? displayNameKey = null,
        double minimumExpandedHeight = 300)
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

        if (!double.IsFinite(minimumExpandedHeight) || minimumExpandedHeight is < 260 or > 420)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumExpandedHeight));
        }

        Id = id;
        DisplayName = displayName;
        Priority = priority;
        CompactViewKey = compactViewKey;
        ExpandedViewKey = expandedViewKey;
        SupportedEvents = supportedEvents;
        DefaultDisplayDuration = defaultDisplayDuration;
        InteractionCommands = interactionCommands ?? Array.Empty<ModuleCommandDescriptor>();
        NotificationViewKey = string.IsNullOrWhiteSpace(notificationViewKey)
            ? compactViewKey
            : notificationViewKey;
        PersistentPriority = persistentPriority ?? priority;
        IsPersistent = isPersistent;
        HoverViewKey = string.IsNullOrWhiteSpace(hoverViewKey) ? compactViewKey : hoverViewKey;
        IconGlyph = string.IsNullOrWhiteSpace(iconGlyph) ? "\uE10C" : iconGlyph;
        DisplayNameKey = string.IsNullOrWhiteSpace(displayNameKey)
            ? $"Module.{id}.Name"
            : displayNameKey;
        MinimumExpandedHeight = minimumExpandedHeight;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string DisplayNameKey { get; }

    public int Priority { get; }

    public string CompactViewKey { get; }

    public string ExpandedViewKey { get; }

    public IReadOnlySet<ModuleEventKind> SupportedEvents { get; }

    public TimeSpan DefaultDisplayDuration { get; }

    public IReadOnlyList<ModuleCommandDescriptor> InteractionCommands { get; }

    public string NotificationViewKey { get; }

    public int PersistentPriority { get; }

    public bool IsPersistent { get; }

    public string HoverViewKey { get; }

    public string IconGlyph { get; }

    public double MinimumExpandedHeight { get; }
}
