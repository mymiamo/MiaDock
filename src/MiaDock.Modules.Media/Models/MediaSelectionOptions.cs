namespace MiaDock.Modules.Media.Models;

public sealed record MediaSelectionOptions(
    string? SelectedSourceId,
    MediaFallbackBehavior FallbackBehavior)
{
    public static MediaSelectionOptions FollowSystemCurrent { get; } =
        new(null, MediaFallbackBehavior.UseAnotherActiveSession);
}
