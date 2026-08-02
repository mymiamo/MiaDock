namespace MiaDock.Platform.Windows.Interop;

public static class WindowStylePolicy
{
    public const long ToolWindow = NativeConstants.WsExToolWindow;
    public const long AppWindow = NativeConstants.WsExAppWindow;
    public const long NoActivate = NativeConstants.WsExNoActivate;
    public const long Layered = NativeConstants.WsExLayered;
    public const long Caption = NativeConstants.WsCaption;
    public const long ThickFrame = NativeConstants.WsThickFrame;
    public const long SystemMenu = NativeConstants.WsSysMenu;
    public const long MinimizeBox = NativeConstants.WsMinimizeBox;
    public const long MaximizeBox = NativeConstants.WsMaximizeBox;

    public static long ApplyOverlayStyles(long currentStyles) =>
        ApplyOverlayStyles(currentStyles, allowActivation: false);

    public static long ApplyOverlayStyles(long currentStyles, bool allowActivation)
    {
        var styles = (currentStyles | ToolWindow) & ~(AppWindow | Layered);
        return allowActivation ? styles & ~NoActivate : styles | NoActivate;
    }

    public static long ApplyOverlayWindowStyles(long currentStyles) =>
        currentStyles & ~(Caption | ThickFrame | SystemMenu | MinimizeBox | MaximizeBox);
}
