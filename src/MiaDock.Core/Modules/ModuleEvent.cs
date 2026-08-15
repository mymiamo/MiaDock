namespace MiaDock.Core.Modules;

public sealed record ModuleEvent
{
    public ModuleEvent(
        string moduleId,
        ModuleEventKind kind,
        ModulePresentation presentation,
        TimeSpan displayDuration,
        DateTimeOffset occurredAt,
        ModuleEventPriority priority = ModuleEventPriority.Normal,
        string? coalescingKey = null,
        DateTimeOffset? expiresAtUtc = null,
        bool isFullscreenEligible = true,
        AudibleNotificationCue audibleCue = AudibleNotificationCue.None)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(presentation);
        if (displayDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(displayDuration));
        }

        ModuleId = moduleId;
        Kind = kind;
        Presentation = presentation;
        DisplayDuration = displayDuration;
        OccurredAt = occurredAt;
        Priority = priority;
        CoalescingKey = string.IsNullOrWhiteSpace(coalescingKey)
            ? $"{moduleId}:{kind}"
            : coalescingKey;
        ExpiresAtUtc = expiresAtUtc ?? occurredAt.ToUniversalTime().Add(displayDuration);
        IsFullscreenEligible = isFullscreenEligible;
        AudibleCue = audibleCue;
    }

    public string ModuleId { get; }
    public ModuleEventKind Kind { get; }
    public ModulePresentation Presentation { get; }
    public TimeSpan DisplayDuration { get; }
    public DateTimeOffset OccurredAt { get; }
    public ModuleEventPriority Priority { get; }
    public string CoalescingKey { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    public bool IsFullscreenEligible { get; }
    public AudibleNotificationCue AudibleCue { get; }
}
