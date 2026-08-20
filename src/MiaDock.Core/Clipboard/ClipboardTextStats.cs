using System.Globalization;

namespace MiaDock.Core.Clipboard;

public sealed record ClipboardTextStats(int CharacterCount, int WordCount, int LineCount)
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
        var characters = new StringInfo(text).LengthInTextElements;
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var lines = text.Length == 0 ? 0 : text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n').Length;
        return new(characters, words, lines);
    }
}
