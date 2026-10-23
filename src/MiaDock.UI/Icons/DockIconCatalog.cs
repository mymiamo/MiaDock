using System.Collections.Frozen;

namespace MiaDock.UI.Icons;

/// <summary>Dock-only adapter for the existing module glyph contract.</summary>
public static class DockIconCatalog
{
    public const string Version = "7.4.0";
    public const string FallbackKey = "help-circle";

    public static IReadOnlyDictionary<string, string> Glyphs { get; } = new Dictionary<string, string>
    {
        ["\uE10C"] = "apps", ["\uE121"] = "time",
        ["\uE701"] = "wifi", ["\uE702"] = "bluetooth", ["\uE708"] = "moon",
        ["\uE70F"] = "pencil", ["\uE710"] = "add-circle", ["\uE711"] = "close-circle",
        ["\uE712"] = "ellipsis-horizontal-circle", ["\uE713"] = "settings", ["\uE714"] = "videocam",
        ["\uE715"] = "mail", ["\uE717"] = "call", ["\uE720"] = "mic",
        ["\uE72E"] = "lock-closed", ["\uE734"] = "star", ["\uE738"] = "remove-circle",
        ["\uE73E"] = "checkmark-circle", ["\uE74A"] = "arrow-up-circle", ["\uE74B"] = "arrow-down-circle",
        ["\uE74D"] = "remove-circle", ["\uE74E"] = "trash", ["\uE74F"] = "volume-mute",
        ["\uE765"] = "keypad", ["\uE767"] = "volume-high", ["\uE768"] = "play", ["\uE769"] = "pause",
        ["\uE76B"] = "chevron-back-circle", ["\uE76C"] = "chevron-forward-circle", ["\uE774"] = "globe",
        ["\uE777"] = "refresh-circle", ["\uE783"] = "warning", ["\uE790"] = "color-palette",
        ["\uE7B2"] = "log-out", ["\uE7BA"] = "refresh-circle", ["\uE7BF"] = "bag",
        ["\uE7C1"] = "flag", ["\uE7C2"] = "move", ["\uE7C4"] = "arrow-up-circle",
        ["\uE7E8"] = "power", ["\uE7ED"] = "add-circle", ["\uE7F4"] = "desktop",
        ["\uE7F6"] = "headset", ["\uE7F8"] = "unlink", ["\uE7FC"] = "game-controller",
        ["\uE80F"] = "home", ["\uE821"] = "briefcase", ["\uE823"] = "timer", ["\uE82D"] = "book",
        ["\uE838"] = "pin", ["\uE839"] = "git-network", ["\uE83E"] = "battery-charging",
        ["\uE850"] = "battery-half", ["\uE88E"] = "hardware-chip", ["\uE890"] = "copy",
        ["\uE892"] = "play-skip-back", ["\uE893"] = "play-skip-forward", ["\uE895"] = "cloud-download",
        ["\uE896"] = "download", ["\uE898"] = "swap-horizontal", ["\uE8A5"] = "document-text",
        ["\uE8A7"] = "open", ["\uE8B7"] = "folder", ["\uE8BD"] = "chatbubble-ellipses",
        ["\uE8BE"] = "leaf", ["\uE8C8"] = "clipboard", ["\uE8D2"] = "arrow-up-circle",
        ["\uE8D6"] = "musical-notes", ["\uE8E5"] = "folder-open", ["\uE8EF"] = "keypad",
        ["\uE916"] = "stopwatch", ["\uE946"] = "information-circle", ["\uE95E"] = "fitness",
        ["\uE962"] = "hardware-chip", ["\uE968"] = "wifi", ["\uE993"] = "volume-low", ["\uE995"] = "volume-medium",
        ["\uEA39"] = "warning", ["\uEA8F"] = "notifications", ["\uEB9F"] = "image", ["\uEC05"] = "cellular"
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public static IReadOnlySet<string> Keys { get; } = Glyphs.Values.Append(FallbackKey).ToFrozenSet(StringComparer.Ordinal);

    public static string Resolve(string? glyph, string? iconKey = null) =>
        iconKey is not null && Keys.Contains(iconKey) ? iconKey :
        glyph is not null && Glyphs.TryGetValue(glyph, out var key) ? key : FallbackKey;

    public static string AssetName(string key, bool filled) =>
        $"{(Keys.Contains(key) ? key : FallbackKey)}{(filled ? string.Empty : "-outline")}.svg";
}
