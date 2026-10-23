using System.Globalization;
using System.Text.RegularExpressions;

namespace MiaDock.Platform.Windows.Logging;

public sealed partial class SensitiveDataRedactor
{
    private static readonly HashSet<string> AllowedPropertyKeys = new(StringComparer.Ordinal)
    {
        "api", "count", "durationMs", "eventKind", "hresult", "moduleId", "operation",
        "reason", "state", "status", "windowKind", "displayMode", "droppedCount",
        "isFullscreen", "source", "phase", "generation", "topologyGeneration",
        "trackRevision", "sequence", "sessionCount", "selected", "matchedCount",
        "failureCount", "retry", "cancellationReason", "packageVersion",
        "osVersion", "architecture", "processArchitecture", "processId", "format",
        "sessionPeak", "packetCount", "silentPacketCount", "maximumSample",
        "fallbackReason", "sourceMode", "previousSourceMode"
    };

    private readonly string _userName;
    private readonly string _userProfile;

    public SensitiveDataRedactor(string? userName = null, string? userProfile = null)
    {
        _userName = userName ?? Environment.UserName;
        _userProfile = userProfile ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public string Redact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Mask complete paths before usernames. "User" must not change the
        // literal C:\Users prefix or leave a filename after a profile marker.
        var redacted = WindowsUserPathRegex().Replace(value, "<user-path>");
        redacted = WindowsAbsolutePathRegex().Replace(redacted, match =>
            !string.IsNullOrWhiteSpace(_userProfile) &&
            match.Value.StartsWith(_userProfile, StringComparison.OrdinalIgnoreCase)
                ? "<user-path>" : "<path>");
        redacted = UnixUserPathRegex().Replace(redacted, "<user-path>");
        if (!string.IsNullOrWhiteSpace(_userName))
        {
            var parts = RedactionTokenRegex().Split(redacted);
            redacted = string.Concat(parts.Select(part => part is "<user-path>" or "<path>" or "<user>"
                ? part : part.Replace(_userName, "<user>", StringComparison.OrdinalIgnoreCase)));
        }
        return redacted.Length <= 4096 ? redacted : redacted[..4096];
    }

    public IReadOnlyDictionary<string, string>? SanitizeProperties(
        IReadOnlyDictionary<string, object?>? properties)
    {
        if (properties is null || properties.Count == 0)
        {
            return null;
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in properties)
        {
            if (!AllowedPropertyKeys.Contains(key) || value is null)
            {
                continue;
            }

            var text = value is IFormattable formattable
                ? formattable.ToString(null, CultureInfo.InvariantCulture)
                : value.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                result[key] = Redact(text);
            }
        }

        return result.Count == 0 ? null : result;
    }

    [GeneratedRegex(@"(?i)[a-z]:\\Users\\[^\\\s]+(?:\\[^\r\n\t\""']*)?")]
    private static partial Regex WindowsUserPathRegex();

    [GeneratedRegex(@"(?i)(?:[a-z]:\\|\\\\)[^\r\n\t\""']+")]
    private static partial Regex WindowsAbsolutePathRegex();

    [GeneratedRegex(@"(?i)/(?:Users|home)/[^/\s]+(?:/[^\r\n\t\""']*)?")]
    private static partial Regex UnixUserPathRegex();

    [GeneratedRegex(@"(<(?:user-path|path|user)>)")]
    private static partial Regex RedactionTokenRegex();
}
