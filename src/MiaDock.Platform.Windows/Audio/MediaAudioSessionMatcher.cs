namespace MiaDock.Platform.Windows.Audio;

public static class MediaAudioSessionMatcher
{
    private static readonly HashSet<string> GenericProcessNames = new(StringComparer.Ordinal)
    {
        "app",
        "applicationframehost",
        "audiodg",
        "svchost",
        "system"
    };
    private static readonly IReadOnlyDictionary<string, string[]> ProcessAliases =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["msedge"] = ["microsoftedge", "edge"]
        };

    public static bool IsMatch(string? mediaSourceId, string? processName)
    {
        if (string.IsNullOrWhiteSpace(mediaSourceId) || string.IsNullOrWhiteSpace(processName))
        {
            return false;
        }

        var source = Normalize(mediaSourceId);
        var process = Normalize(processName);
        if (source.Length < 3 || process.Length < 3 || GenericProcessNames.Contains(process))
        {
            return false;
        }

        return source == process ||
               source.Contains(process, StringComparison.Ordinal) ||
               process.Contains(source, StringComparison.Ordinal) ||
               ProcessAliases.TryGetValue(process, out var aliases) &&
               aliases.Any(alias => source.Contains(alias, StringComparison.Ordinal));
    }

    internal static string Normalize(string value)
    {
        var candidate = value.Trim();
        var lastSeparator = Math.Max(candidate.LastIndexOf('\\'), candidate.LastIndexOf('/'));
        if (lastSeparator >= 0 && lastSeparator + 1 < candidate.Length)
        {
            candidate = candidate[(lastSeparator + 1)..];
        }

        if (candidate.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[..^4];
        }

        return new string(candidate
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}
