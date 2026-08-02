namespace MiaDock.Core.Presentation;

public sealed record IslandMotionOptions(
    TimeSpan HoverDuration,
    TimeSpan ExpandDuration,
    TimeSpan CollapseDuration,
    TimeSpan NotificationEnterDuration,
    TimeSpan NotificationExitDuration,
    TimeSpan ContentRefreshDuration,
    TimeSpan PointerExitDelay,
    TimeSpan NotificationVisibleDuration,
    TimeSpan ExpandedInactivityDuration,
    MotionPreset Preset,
    double Intensity,
    double Springiness,
    TimeSpan ContentDelay,
    bool EnableParallax,
    bool EnableTransientBlur)
{
    public static IslandMotionOptions Default { get; } = new(
        TimeSpan.FromMilliseconds(170),
        TimeSpan.FromMilliseconds(240),
        TimeSpan.FromMilliseconds(180),
        TimeSpan.FromMilliseconds(220),
        TimeSpan.FromMilliseconds(160),
        TimeSpan.FromMilliseconds(120),
        TimeSpan.FromMilliseconds(250),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(8),
        MotionPreset.Balanced,
        0.7,
        0.55,
        TimeSpan.FromMilliseconds(35),
        false,
        false);

    public void Validate()
    {
        ValidateDuration(HoverDuration, nameof(HoverDuration), allowZero: true);
        ValidateDuration(ExpandDuration, nameof(ExpandDuration), allowZero: true);
        ValidateDuration(CollapseDuration, nameof(CollapseDuration), allowZero: true);
        ValidateDuration(NotificationEnterDuration, nameof(NotificationEnterDuration), allowZero: true);
        ValidateDuration(NotificationExitDuration, nameof(NotificationExitDuration), allowZero: true);
        ValidateDuration(ContentRefreshDuration, nameof(ContentRefreshDuration), allowZero: true);
        ValidateDuration(PointerExitDelay, nameof(PointerExitDelay), allowZero: true);
        ValidateDuration(NotificationVisibleDuration, nameof(NotificationVisibleDuration), allowZero: false);
        ValidateDuration(ExpandedInactivityDuration, nameof(ExpandedInactivityDuration), allowZero: false);
        ValidateDuration(ContentDelay, nameof(ContentDelay), allowZero: true);
        if (!Enum.IsDefined(Preset))
        {
            throw new ArgumentOutOfRangeException(nameof(Preset));
        }
        if (!double.IsFinite(Intensity) || Intensity is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Intensity));
        }
        if (!double.IsFinite(Springiness) || Springiness is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Springiness));
        }
    }

    private static void ValidateDuration(TimeSpan value, string name, bool allowZero)
    {
        if (value < TimeSpan.Zero || (!allowZero && value == TimeSpan.Zero) || value > TimeSpan.FromMinutes(1))
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}
