namespace MiaDock.Core.Settings;

public sealed record GlobalHotKeySettings(
    bool IsEnabled,
    IReadOnlyDictionary<HotKeyAction, HotKeyGestureSetting> Bindings)
{
    public static GlobalHotKeySettings Default { get; } = new(
        false,
        new Dictionary<HotKeyAction, HotKeyGestureSetting>());
}
