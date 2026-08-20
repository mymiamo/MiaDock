using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace MiaDock.Platform.Windows.Tray;

/// <summary>Maps tray command semantics to the selected local Fluent SVG assets.</summary>
internal static class FluentTrayIconResolver
{
    private const string AssetRoot = "ms-appx:///Assets/FluentIcons/";

    internal static string? GetAssetName(TrayIconKey key) => key switch
    {
        TrayIconKey.Window => "window_24_regular.svg",
        TrayIconKey.Settings => "settings_24_regular.svg",
        TrayIconKey.Previous => "arrow_previous_24_regular.svg",
        TrayIconKey.Play => "play_24_regular.svg",
        TrayIconKey.Pause => "pause_24_regular.svg",
        TrayIconKey.Next => "arrow_next_24_regular.svg",
        TrayIconKey.Music => "music_note_2_24_regular.svg",
        TrayIconKey.Notifications => "alert_24_regular.svg",
        TrayIconKey.Monitor => "desktop_24_regular.svg",
        TrayIconKey.Focus => "eye_off_24_regular.svg",
        TrayIconKey.Exit => "power_24_regular.svg",
        _ => null
    };

    internal static IconElement? Create(TrayIconKey key)
    {
        var asset = GetAssetName(key);
        return asset is null
            ? null
            : new ImageIcon
            {
                Source = new SvgImageSource(new Uri(AssetRoot + asset))
            };
    }
}
