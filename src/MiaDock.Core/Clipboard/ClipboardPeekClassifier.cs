using System.Text.RegularExpressions;

namespace MiaDock.Core.Clipboard;

public static partial class ClipboardPeekClassifier
{
    public static ClipboardPeekItem ClassifyText(string? text, DateTimeOffset createdAt)
    {
        var value = text?.Trim() ?? string.Empty;
        if (IsSensitive(value)) return Sensitive(value, createdAt);
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return new(Guid.NewGuid().ToString("N"), ClipboardPeekContentType.Url,
                Truncate(uri.Host + uri.PathAndQuery), value, createdAt, "Text", false,
                ClipboardPeekCapabilities.Copy | ClipboardPeekCapabilities.Open,
                uri);
        }
        if (EmailRegex().IsMatch(value))
        {
            return new(Guid.NewGuid().ToString("N"), ClipboardPeekContentType.Email, value, value, createdAt, "Text", false,
                ClipboardPeekCapabilities.Copy | ClipboardPeekCapabilities.ComposeEmail,
                EmailAddress: value);
        }
        if (TryNormalizeColor(value, out var color))
        {
            return new(Guid.NewGuid().ToString("N"), ClipboardPeekContentType.Color, color, color, createdAt, "Text", false,
                ClipboardPeekCapabilities.Copy, ColorValue: color);
        }
        if (TryGetExistingPath(value, out var path, out var isFolder))
        {
            return new(Guid.NewGuid().ToString("N"), isFolder ? ClipboardPeekContentType.Folder : ClipboardPeekContentType.File,
                Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), path, createdAt, "Text", false,
                ClipboardPeekCapabilities.Copy | ClipboardPeekCapabilities.Open | ClipboardPeekCapabilities.OpenFolder,
                FilePath: path);
        }
        return new(Guid.NewGuid().ToString("N"), string.IsNullOrEmpty(value) ? ClipboardPeekContentType.Unknown : ClipboardPeekContentType.PlainText,
            Truncate(value), value, createdAt, "Text", false,
            ClipboardPeekCapabilities.Copy);
    }

    public static ClipboardPeekItem Sensitive(string rawValue, DateTimeOffset createdAt) => new(
        Guid.NewGuid().ToString("N"), ClipboardPeekContentType.Sensitive, "••••••••", null, createdAt,
        "Text", true, ClipboardPeekCapabilities.Reveal);

    public static bool IsSensitive(string value) =>
        !string.IsNullOrWhiteSpace(value) && (PrivateKeyRegex().IsMatch(value) || ApiTokenRegex().IsMatch(value) ||
            JwtRegex().IsMatch(value) || BearerTokenRegex().IsMatch(value) || StandaloneOtpRegex().IsMatch(value) ||
            IsPaymentCard(value) || LooksLikeHighEntropySecret(value));

    public static bool TryNormalizeColor(string value, out string color)
    {
        color = string.Empty;
        if (!ClipboardColorFormats.TryParse(value, out var formats)) return false;
        color = formats.Hex;
        return true;
    }

    private static bool TryGetExistingPath(string value, out string path, out bool isFolder)
    {
        path = string.Empty;
        isFolder = false;
        if (!Path.IsPathFullyQualified(value)) return false;
        try
        {
            var normalized = Path.GetFullPath(value);
            if (File.Exists(normalized)) { path = normalized; return true; }
            if (Directory.Exists(normalized)) { path = normalized; isFolder = true; return true; }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException) { }
        return false;
    }

    private static string Truncate(string value) => value.Length <= 160 ? value : value[..157] + "…";

    private static bool LooksLikeHighEntropySecret(string value)
    {
        if (value.Length is < 32 or > 256 || value.Any(char.IsWhiteSpace) ||
            !value.All(character => char.IsLetterOrDigit(character) || character is '+' or '/' or '_' or '-' or '=')) return false;
        if (value.Distinct().Count() < 12 || !value.Any(char.IsDigit) || !value.Any(char.IsLetter)) return false;
        var entropy = value.GroupBy(character => character)
            .Select(group => group.Count() / (double)value.Length)
            .Sum(probability => -probability * Math.Log2(probability));
        return entropy >= 4.0;
    }

    private static bool IsPaymentCard(string value)
    {
        if (!PaymentCardRegex().IsMatch(value)) return false;
        var digits = value.Where(char.IsDigit).Select(character => character - '0').ToArray();
        if (digits.Length is < 13 or > 19 || digits.Distinct().Count() == 1) return false;
        var sum = 0;
        var doubleDigit = false;
        for (var index = digits.Length - 1; index >= 0; index--)
        {
            var digit = digits[index];
            if (doubleDigit && (digit *= 2) > 9) digit -= 9;
            sum += digit;
            doubleDigit = !doubleDigit;
        }
        return sum % 10 == 0;
    }

    [GeneratedRegex("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", RegexOptions.CultureInvariant)] private static partial Regex EmailRegex();
    [GeneratedRegex("-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----", RegexOptions.CultureInvariant)] private static partial Regex PrivateKeyRegex();
    [GeneratedRegex("^(?:sk-[A-Za-z0-9_-]{20,}|gh[pousr]_[A-Za-z0-9]{20,}|AKIA[0-9A-Z]{16})$", RegexOptions.CultureInvariant)] private static partial Regex ApiTokenRegex();
    [GeneratedRegex("^[A-Za-z0-9_-]{8,}\\.[A-Za-z0-9_-]{8,}\\.[A-Za-z0-9_-]{8,}$", RegexOptions.CultureInvariant)] private static partial Regex JwtRegex();
    [GeneratedRegex("^Bearer\\s+[A-Za-z0-9._~+/-]{16,}={0,2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex BearerTokenRegex();
    [GeneratedRegex("^\\d{6}$", RegexOptions.CultureInvariant)] private static partial Regex StandaloneOtpRegex();
    [GeneratedRegex("^[0-9][0-9 -]{11,23}[0-9]$", RegexOptions.CultureInvariant)] private static partial Regex PaymentCardRegex();
}
