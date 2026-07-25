using System.Runtime.InteropServices;

namespace MiaDock.Platform.Windows.Tray;

internal static class TrayNative
{
    internal const uint WmApp = 0x8000;
    internal const uint WmNull = 0x0000;
    internal const uint WmContextMenu = 0x007B;
    internal const uint WmLButtonDoubleClick = 0x0203;
    internal const uint WmRButtonUp = 0x0205;
    internal const uint TrayCallbackMessage = WmApp + 42;

    internal const uint NimAdd = 0x00000000;
    internal const uint NimModify = 0x00000001;
    internal const uint NimDelete = 0x00000002;
    internal const uint NimSetVersion = 0x00000004;
    internal const uint NifMessage = 0x00000001;
    internal const uint NifIcon = 0x00000002;
    internal const uint NifTip = 0x00000004;
    internal const uint NotifyIconVersion4 = 4;
    internal const uint ImageIcon = 1;
    internal const uint LrLoadFromFile = 0x00000010;
    internal const uint LrDefaultSize = 0x00000040;

    internal const uint MfString = 0x00000000;
    internal const uint MfGray = 0x00000001;
    internal const uint MfChecked = 0x00000008;
    internal const uint MfPopup = 0x00000010;
    internal const uint MfSeparator = 0x00000800;
    internal const uint TpmRightButton = 0x0002;
    internal const uint TpmReturnCommand = 0x0100;

    internal static readonly nint HwndMessage = new(-3);
    internal static readonly nint IdiApplication = new(32512);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    internal delegate nint WindowProcedure(nint windowHandle, uint message, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WindowClass
    {
        internal uint Size;
        internal uint Style;
        internal WindowProcedure WindowProcedure;
        internal int ClassExtra;
        internal int WindowExtra;
        internal nint Instance;
        internal nint Icon;
        internal nint Cursor;
        internal nint Background;
        internal string? MenuName;
        internal string ClassName;
        internal nint SmallIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct Point
    {
        internal int X;
        internal int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct NotifyIconData
    {
        internal uint Size;
        internal nint WindowHandle;
        internal uint Id;
        internal uint Flags;
        internal uint CallbackMessage;
        internal nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        internal string ToolTip;
        internal uint State;
        internal uint StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        internal string Info;
        internal uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        internal string InfoTitle;
        internal uint InfoFlags;
        internal Guid GuidItem;
        internal nint BalloonIcon;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint GetModuleHandleW(string? moduleName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern ushort RegisterClassExW(ref WindowClass windowClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool UnregisterClassW(string className, nint instance);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateWindowExW(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll")]
    internal static extern bool DestroyWindow(nint windowHandle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern uint RegisterWindowMessageW(string messageName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint DefWindowProcW(nint windowHandle, uint message, nuint wParam, nint lParam);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool Shell_NotifyIconW(uint message, ref NotifyIconData data);

    [DllImport("user32.dll")]
    internal static extern nint LoadIconW(nint instance, nint iconName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint LoadImageW(
        nint instance,
        string name,
        uint type,
        int width,
        int height,
        uint loadFlags);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool DestroyIcon(nint icon);

    [DllImport("user32.dll")]
    internal static extern nint CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern bool AppendMenuW(nint menu, uint flags, nuint item, string? text);

    [DllImport("user32.dll")]
    internal static extern uint TrackPopupMenuEx(
        nint menu,
        uint flags,
        int x,
        int y,
        nint windowHandle,
        nint parameters);

    [DllImport("user32.dll")]
    internal static extern bool DestroyMenu(nint menu);

    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    internal static extern bool SetForegroundWindow(nint windowHandle);

    [DllImport("user32.dll")]
    internal static extern bool PostMessageW(nint windowHandle, uint message, nuint wParam, nint lParam);
}
