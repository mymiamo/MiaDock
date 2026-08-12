using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MiaDock.Platform.Windows.Lifecycle;

public sealed class WindowsSessionLockStateService : IWindowsSessionLockStateService
{
    private const uint WmWtsSessionChange = 0x02B1;
    private const nuint WtsSessionLock = 0x7;
    private const nuint WtsSessionUnlock = 0x8;
    private const uint NotifyForThisSession = 0;

    private readonly WindowProcedure _windowProcedure;
    private readonly string _windowClassName = $"MiaDock.SessionState.{Guid.NewGuid():N}";
    private nint _instance;
    private nint _windowHandle;
    private bool _started;
    private bool _disposed;

    public WindowsSessionLockStateService() => _windowProcedure = HandleWindowMessage;

    public bool IsLocked { get; private set; }

    public event EventHandler<bool>? LockStateChanged;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return;

        _instance = GetModuleHandleW(null);
        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            Instance = _instance,
            WindowProcedure = _windowProcedure,
            ClassName = _windowClassName
        };
        if (RegisterClassExW(ref windowClass) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Session state window class could not be registered.");
        }

        _windowHandle = CreateWindowExW(
            0, _windowClassName, string.Empty, 0,
            0, 0, 0, 0, 0, 0, _instance, 0);
        if (_windowHandle == 0)
        {
            var error = Marshal.GetLastWin32Error();
            UnregisterClassW(_windowClassName, _instance);
            throw new Win32Exception(error, "Session state window could not be created.");
        }

        if (!WTSRegisterSessionNotification(_windowHandle, NotifyForThisSession))
        {
            var error = Marshal.GetLastWin32Error();
            DestroyWindow(_windowHandle);
            _windowHandle = 0;
            UnregisterClassW(_windowClassName, _instance);
            throw new Win32Exception(error, "Session notifications could not be registered.");
        }

        _started = true;
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Unregister session notify, but skip DestroyWindow/UnregisterClass.
        // DestroyWindow re-enters the native WndProc and has fail-fast'd on Exit.
        _disposed = true;
        if (_windowHandle != 0)
        {
            if (_started)
            {
                try
                {
                    WTSUnRegisterSessionNotification(_windowHandle);
                }
                catch (Exception)
                {
                }
            }

            _windowHandle = 0;
        }

        _started = false;
    }

    private nint HandleWindowMessage(nint window, uint message, nuint wParam, nint lParam)
    {
        try
        {
            if (!_disposed &&
                message == WmWtsSessionChange &&
                (wParam == WtsSessionLock || wParam == WtsSessionUnlock))
            {
                var locked = wParam == WtsSessionLock;
                if (IsLocked != locked)
                {
                    IsLocked = locked;
                    LockStateChanged?.Invoke(this, locked);
                }
                return 0;
            }
        }
        catch (Exception)
        {
            // A managed exception must never cross the reverse P/Invoke WndProc
            // boundary; escaping here terminates the process through a native
            // fail-fast path that no handler can observe.
        }

        return DefWindowProcW(window, message, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public WindowProcedure WindowProcedure;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public string? MenuName;
        public string ClassName;
        public nint SmallIcon;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassExW(ref WindowClass windowClass);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowExW(uint extendedStyle, string className, string windowName,
        uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClassW(string className, nint instance);
    [DllImport("user32.dll")]
    private static extern nint DefWindowProcW(nint window, uint message, nuint wParam, nint lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? moduleName);
    [DllImport("wtsapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSRegisterSessionNotification(nint window, uint flags);
    [DllImport("wtsapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSUnRegisterSessionNotification(nint window);
}
