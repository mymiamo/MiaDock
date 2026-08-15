using System.Globalization;

namespace MiaDock.Modules.DeviceStatus.Services;

public static class BluetoothAddressParser
{
    public static bool TryParse(string? value, out ulong address)
    {
        address = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var hex = value.Trim();
        if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            hex = hex[2..];
        hex = hex.Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(".", string.Empty, StringComparison.Ordinal);
        if (hex.Length is not (12 or 16)) return false;
        return ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out address) &&
               address != 0;
    }

    public static string? TryExtractFromEndpointId(string? endpointId)
    {
        if (string.IsNullOrWhiteSpace(endpointId)) return null;
        var separator = endpointId.LastIndexOf('-');
        if (separator < 0 || separator == endpointId.Length - 1) return null;
        var candidate = endpointId[(separator + 1)..];
        return TryParse(candidate, out _) ? candidate : null;
    }
}
