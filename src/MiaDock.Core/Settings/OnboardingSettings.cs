namespace MiaDock.Core.Settings;

public sealed record OnboardingSettings(
    bool IsCompleted,
    int CompletedVersion,
    DateTimeOffset? CompletedAtUtc)
{
    public const int CurrentVersion = 2;

    public static OnboardingSettings Default { get; } = new(false, 0, null);
}
