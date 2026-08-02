namespace MiaDock.Core.Focus;

public sealed record FocusSnapshot(
    IReadOnlyList<FocusProfile> Profiles,
    FocusProfile? ActiveProfile,
    FocusActivationState? ActiveState)
{
    public static FocusSnapshot Empty { get; } = new(
        Array.Empty<FocusProfile>(),
        null,
        null);

    public bool IsActive => ActiveProfile is not null && ActiveState is not null;
}
