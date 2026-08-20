namespace MiaDock.Platform.Windows.Tray;

public enum TrayIconKey
{
    None,
    Window,
    Settings,
    Previous,
    Play,
    Pause,
    Next,
    Music,
    Notifications,
    Monitor,
    Focus,
    Exit
}

public sealed record TrayMenuItem(
    int CommandId,
    string Text,
    bool IsEnabled = true,
    bool IsChecked = false,
    IReadOnlyList<TrayMenuItem>? Children = null,
    bool IsSeparator = false,
    TrayIconKey IconKey = TrayIconKey.None,
    bool IsRadio = false)
{
    public static TrayMenuItem Separator { get; } = new(0, string.Empty, IsSeparator: true);
}
