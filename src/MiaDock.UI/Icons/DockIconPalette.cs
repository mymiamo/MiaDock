using Windows.UI;

namespace MiaDock.UI.Icons;

internal static class DockIconPalette
{
    private static bool _highContrast;
    private static uint _foreground;
    private static uint _highlightText;
    private static uint _highlight;
    internal static event EventHandler? Changed;

    internal static void Update(bool highContrast, Color foreground, Color highlightText, Color highlight)
    {
        var fg = Pack(foreground); var ht = Pack(highlightText); var hl = Pack(highlight);
        if ((_highContrast, _foreground, _highlightText, _highlight) == (highContrast, fg, ht, hl)) return;
        (_highContrast, _foreground, _highlightText, _highlight) = (highContrast, fg, ht, hl);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    // Preserve system hover/accent colors; replace custom profile/status colors
    // with WindowText when Windows high contrast is active.
    internal static uint Resolve(uint requested) => !_highContrast ||
        requested == _foreground || requested == _highlightText || requested == _highlight ? requested : _foreground;
    private static uint Pack(Color color) => ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
}
