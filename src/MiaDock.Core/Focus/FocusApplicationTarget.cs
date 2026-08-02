namespace MiaDock.Core.Focus;

public static class FocusApplicationTarget
{
    public static string Normalize(string target)
    {
        var value = Path.GetFileName(target.Trim());
        if (value.Length == 0)
        {
            return string.Empty;
        }

        return value.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? value.ToLowerInvariant()
            : $"{value.ToLowerInvariant()}.exe";
    }
}
