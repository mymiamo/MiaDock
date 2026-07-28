namespace MiaDock.Core.Updates;

public sealed record StoreUpdateSnapshot(
    StoreUpdateStatus Status,
    Version CurrentVersion,
    Version? AvailableVersion = null,
    DateTimeOffset? CheckedAtUtc = null)
{
    public static StoreUpdateSnapshot Unavailable(Version currentVersion) =>
        new(StoreUpdateStatus.Unavailable, Normalize(currentVersion));

    public static Version Normalize(Version version)
    {
        ArgumentNullException.ThrowIfNull(version);
        return new Version(
            Math.Max(0, version.Major),
            Math.Max(0, version.Minor),
            Math.Max(0, version.Build),
            Math.Max(0, version.Revision));
    }
}
