namespace MiaDock.Core.Focus;

public sealed record FocusActivationOptions(
    FocusActivationSource Source,
    TimeSpan? Duration,
    bool UseProfileDefaultDuration)
{
    public static FocusActivationOptions ProfileDefault(
        FocusActivationSource source = FocusActivationSource.Manual) =>
        new(source, null, true);

    public static FocusActivationOptions ForDuration(
        TimeSpan duration,
        FocusActivationSource source = FocusActivationSource.Manual) =>
        new(source, duration, false);

    public static FocusActivationOptions Indefinite(
        FocusActivationSource source = FocusActivationSource.Manual) =>
        new(source, null, false);
}
