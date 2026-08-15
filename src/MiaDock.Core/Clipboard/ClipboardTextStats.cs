namespace MiaDock.Core.Clipboard;

public sealed record ClipboardTextStats(int CharacterCount, int WordCount)
{
    public static ClipboardTextStats? TryCreate(ClipboardPeekItem? item)
    {
        if (item is null || item.IsSensitive || item.Type != ClipboardPeekContentType.PlainText)
            return null;
        if (string.IsNullOrEmpty(item.RawText)) return null;
        return FromText(item.RawText);
    }

    public static ClipboardTextStats FromText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new(text.Length, text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length);
    }
}
