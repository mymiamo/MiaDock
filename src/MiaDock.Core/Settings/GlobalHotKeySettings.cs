namespace MiaDock.Core.Settings;

public sealed record GlobalHotKeySettings(
    bool IsEnabled,
    IReadOnlyDictionary<HotKeyAction, HotKeyGestureSetting> Bindings)
{
    public static IReadOnlyDictionary<HotKeyAction, HotKeyGestureSetting> RecommendedBindings { get; } =
        new Dictionary<HotKeyAction, HotKeyGestureSetting>
        {
            [HotKeyAction.ToggleDock] = Gesture(0x44),          // D
            [HotKeyAction.ToggleExpanded] = Gesture(0x45),      // E
            [HotKeyAction.NextModule] = Gesture(0x4E),          // N
            [HotKeyAction.MediaPlayPause] = Gesture(0x50),      // P
            [HotKeyAction.TimerPauseResume] = Gesture(0x54)     // T
        };

    public static GlobalHotKeySettings Default { get; } = new(
        false,
        new Dictionary<HotKeyAction, HotKeyGestureSetting>());

    public static HotKeyGestureSetting RecommendedFor(HotKeyAction action) =>
        RecommendedBindings[action];

    private static HotKeyGestureSetting Gesture(int virtualKey) => new(
        HotKeyModifiers.Control | HotKeyModifiers.Alt | HotKeyModifiers.Shift,
        virtualKey);
}
