namespace MiaDock.Core.Input;

public sealed record KeyboardLockStateChangedEventArgs(
    KeyboardLockKind Kind,
    bool IsOn,
    DateTimeOffset OccurredAtUtc);
