using System.Security.Cryptography;
using System.Text;
using MiaDock.Modules.Transfers.Models;

namespace MiaDock.Modules.Transfers;

public static class TransferProtocol
{
    public const int CurrentVersion = 1;
    public const int MaximumMessageBytes = 64 * 1024;
    public const int MaximumUpdatesPerSecond = 10;
    public const int MaximumProviderIdLength = 64;
    public const int MaximumTransferIdLength = 128;
    public const int MaximumDisplayNameLength = 128;

    public static string CurrentUserPipeName
    {
        get
        {
            var identity = $"{Environment.UserDomainName}\\{Environment.UserName}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
            return $"MiaDock.TransferProgress.{Convert.ToHexString(hash.AsSpan(0, 8))}";
        }
    }

    public static bool TryNormalize(
        TransferProgressMessage? message,
        out TransferProgressMessage normalized)
    {
        normalized = default!;
        if (message is null ||
            message.ProtocolVersion != CurrentVersion ||
            !IsValidIdentifier(message.ProviderId, MaximumProviderIdLength) ||
            !IsValidIdentifier(message.TransferId, MaximumTransferIdLength) ||
            message.TransferredBytes < 0 ||
            message.TotalBytes < 0 ||
            (message.TotalBytes > 0 && message.TransferredBytes > message.TotalBytes) ||
            !Enum.IsDefined(message.Status))
        {
            return false;
        }

        var safeName = NormalizeDisplayName(message.SafeDisplayName);
        if (safeName.Length == 0)
        {
            return false;
        }

        normalized = message with
        {
            ProviderId = message.ProviderId.Trim(),
            TransferId = message.TransferId.Trim(),
            SafeDisplayName = safeName,
            TimestampUtc = message.TimestampUtc.ToUniversalTime()
        };
        return true;
    }

    private static bool IsValidIdentifier(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var trimmed = value.Trim();
        return trimmed.Length <= maximumLength && trimmed.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
    }

    private static string NormalizeDisplayName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var cleaned = new string(value.Trim().Where(character => !char.IsControl(character)).ToArray());
        return cleaned.Length <= MaximumDisplayNameLength
            ? cleaned
            : cleaned[..MaximumDisplayNameLength];
    }
}
