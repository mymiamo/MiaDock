namespace MiaDock.Core.Logging;

public sealed record LogRetentionPolicy(long MaximumFileBytes, int MaximumFiles, TimeSpan MaximumAge)
{
    public static LogRetentionPolicy Default { get; } = new(
        2 * 1024 * 1024,
        10,
        TimeSpan.FromDays(14));

    public void Validate()
    {
        if (MaximumFileBytes < 64 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumFileBytes));
        }

        if (MaximumFiles is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumFiles));
        }

        if (MaximumAge < TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumAge));
        }
    }
}
