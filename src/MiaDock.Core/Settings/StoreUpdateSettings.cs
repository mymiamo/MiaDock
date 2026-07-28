namespace MiaDock.Core.Settings;

public sealed record StoreUpdateSettings(
    bool AutomaticChecksEnabled,
    DateTimeOffset? LastCheckUtc,
    string? LastNotifiedVersion)
{
    public static StoreUpdateSettings Default { get; } = new(
        true,
        null,
        null);
}
