namespace MiaDock.Core.Focus;

public sealed record FocusAutomationCandidate(
    string ProfileId,
    string TriggerKey,
    FocusActivationSource Source,
    int Priority);
