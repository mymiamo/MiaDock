namespace MiaDock.Platform.Windows.Interop;

internal static class NativeConstants
{
    internal const int GwlStyle = -16;
    internal const int GwlExStyle = -20;

    internal const long WsCaption = 0x00C00000L;
    internal const long WsThickFrame = 0x00040000L;
    internal const long WsSysMenu = 0x00080000L;
    internal const long WsMinimizeBox = 0x00020000L;
    internal const long WsMaximizeBox = 0x00010000L;
    internal const long WsOverlappedWindow = WsCaption | WsThickFrame | WsSysMenu | WsMinimizeBox | WsMaximizeBox;

    internal const long WsExToolWindow = 0x00000080L;
    internal const long WsExAppWindow = 0x00040000L;
    internal const long WsExNoActivate = 0x08000000L;
    internal const long WsExLayered = 0x00080000L;

    internal const uint WmSettingChange = 0x001A;
    internal const uint WmMouseActivate = 0x0021;
    internal const uint WmDisplayChange = 0x007E;
    internal const uint WmNcDestroy = 0x0082;
    internal const uint WmThemeChanged = 0x031A;
    internal const uint WmDwmCompositionChanged = 0x031E;
    internal const uint WmDpiChanged = 0x02E0;
    internal const uint WmLeftButtonDown = 0x0201;
    internal const uint WmRightButtonDown = 0x0204;
    internal const uint WmMiddleButtonDown = 0x0207;
    internal const uint WmXButtonDown = 0x020B;

    internal const int WhMouseLowLevel = 14;

    internal const uint EventSystemForeground = 0x0003;
    internal const uint EventObjectLocationChange = 0x800B;
    internal const int ObjectIdWindow = 0;
    internal const uint WinEventOutOfContext = 0x0000;
    internal const uint WinEventSkipOwnProcess = 0x0002;
    internal const uint MonitorDefaultToNearest = 0x00000002;

    internal const int DwmwaNcRenderingPolicy = 2;
    internal const int DwmwaWindowCornerPreference = 33;
    internal const int DwmwaBorderColor = 34;
    internal const int DwmwaExtendedFrameBounds = 9;
    internal const int DwmwaCloaked = 14;

    internal const uint DwmNcRenderingDisabled = 1;
    internal const uint DwmWindowCornerDoNotRound = 1;
    internal const uint DwmColorNone = 0xFFFFFFFE;

    internal const nint MaNoActivate = 3;
    internal const int SwShowNoActivate = 4;
    internal const int SwHide = 0;
    internal const uint LwaAlpha = 0x00000002;

    internal const uint SwpNoSize = 0x0001;
    internal const uint SwpNoMove = 0x0002;
    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpFrameChanged = 0x0020;
    internal const uint SwpShowWindow = 0x0040;
    internal const uint SwpNoOwnerZOrder = 0x0200;

    internal static readonly nint HwndTopmost = new(-1);
}
