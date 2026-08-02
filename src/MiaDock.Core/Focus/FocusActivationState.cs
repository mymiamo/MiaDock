namespace MiaDock.Core.Focus;

public sealed record FocusActivationState(
    string ProfileId,
    FocusActivationSource Source,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? EndsAtUtc);
