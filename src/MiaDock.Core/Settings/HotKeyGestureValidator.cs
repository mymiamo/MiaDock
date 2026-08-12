namespace MiaDock.Core.Settings;

public static class HotKeyGestureValidator
{
    private static readonly HashSet<int> ModifierKeys =
    [
        0x10, 0x11, 0x12, // Shift, Control, Alt
        0x5B, 0x5C,       // Windows
        0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5
    ];

    public static bool IsValid(HotKeyGestureSetting? gesture)
    {
        if (gesture is null || gesture.VirtualKey is <= 0 or > 0xFF)
        {
            return false;
        }

        if (gesture.VirtualKey == 0x7B || ModifierKeys.Contains(gesture.VirtualKey))
        {
            return false;
        }

        var allowed = HotKeyModifiers.Alt | HotKeyModifiers.Control | HotKeyModifiers.Shift;
        return gesture.Modifiers != HotKeyModifiers.None &&
               (gesture.Modifiers & ~allowed) == HotKeyModifiers.None;
    }

    public static bool IsDuplicate(
        IReadOnlyDictionary<HotKeyAction, HotKeyGestureSetting> bindings,
        HotKeyAction action,
        HotKeyGestureSetting gesture)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(gesture);
        return bindings.Any(pair => pair.Key != action && pair.Value == gesture);
    }
}
