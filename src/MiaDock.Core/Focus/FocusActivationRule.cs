namespace MiaDock.Core.Focus;

public sealed record FocusActivationRule(
    string Id,
    bool IsEnabled,
    FocusActivationRuleKind Kind,
    string? Target);
