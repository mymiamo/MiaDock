using System.ComponentModel;
using System.Runtime.InteropServices;
using MiaDock.Core.Settings;

namespace MiaDock.Platform.Windows.HotKeys;

public sealed class WindowsGlobalHotKeyService : IGlobalHotKeyService
{
    private const uint WmHotKey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private static readonly nint HwndMessage = new(-3);

    private readonly WindowProcedure _windowProcedure;
    private readonly string _windowClassName = $"MiaDock.HotKeys.{Guid.NewGuid():N}";
    private readonly Dictionary<int, HotKeyAction> _registrations = [];
    private nint _instance;
    private nint _windowHandle;
    private bool _initialized;
    private bool _disposed;

    public WindowsGlobalHotKeyService() => _windowProcedure = HandleWindowMessage;

    public event EventHandler<HotKeyAction>? Invoked;
    public event EventHandler? RegistrationsChanged;

    public IReadOnlyDictionary<HotKeyAction, HotKeyRegistrationStatus> RegistrationStatuses { get; private set; } =
        new Dictionary<HotKeyAction, HotKeyRegistrationStatus>();

    public IReadOnlyDictionary<HotKeyAction, HotKeyRegistrationStatus> Apply(GlobalHotKeySettings settings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(settings);
        EnsureInitialized();
        ClearRegistrations();

        var result = new Dictionary<HotKeyAction, HotKeyRegistrationStatus>();
        foreach (var action in Enum.GetValues<HotKeyAction>())
        {
            if (!settings.IsEnabled || !settings.Bindings.TryGetValue(action, out var gesture))
            {
                result[action] = HotKeyRegistrationStatus.Disabled;
                continue;
            }

            if (!HotKeyGestureValidator.IsValid(gesture))
            {
                result[action] = HotKeyRegistrationStatus.Invalid;
                continue;
            }

            var id = 0x5100 + (int)action;
            var modifiers = ToNativeModifiers(gesture.Modifiers) | ModNoRepeat;
            if (!RegisterHotKey(_windowHandle, id, modifiers, checked((uint)gesture.VirtualKey)))
            {
                result[action] = HotKeyRegistrationStatus.Conflict;
                continue;
            }

            _registrations[id] = action;
            result[action] = HotKeyRegistrationStatus.Registered;
        }

        RegistrationStatuses = result;
        RegistrationsChanged?.Invoke(this, EventArgs.Empty);
        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        ClearRegistrations();
        if (_windowHandle != 0)
        {
            DestroyWindow(_windowHandle);
            _windowHandle = 0;
        }

        if (_initialized && _instance != 0)
        {
            UnregisterClassW(_windowClassName, _instance);
        }

        _disposed = true;
    }

    private void EnsureInitialized()
    {
        if (_initialized) return;
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
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Kısayol mesaj penceresi kaydedilemedi.");
        }

        _windowHandle = CreateWindowExW(0, _windowClassName, string.Empty, 0, 0, 0, 0, 0,
            HwndMessage, 0, _instance, 0);
        if (_windowHandle == 0)
        {
            var error = Marshal.GetLastWin32Error();
            UnregisterClassW(_windowClassName, _instance);
            throw new Win32Exception(error, "Kısayol mesaj penceresi oluşturulamadı.");
        }

        _initialized = true;
    }

    private void ClearRegistrations()
    {
        foreach (var id in _registrations.Keys)
        {
            UnregisterHotKey(_windowHandle, id);
        }
        _registrations.Clear();
    }

    private nint HandleWindowMessage(nint window, uint message, nuint wParam, nint lParam)
    {
        if (message == WmHotKey && _registrations.TryGetValue(checked((int)wParam), out var action))
        {
            Invoked?.Invoke(this, action);
            return 0;
        }

        return DefWindowProcW(window, message, wParam, lParam);
    }

    private static uint ToNativeModifiers(HotKeyModifiers value)
    {
        uint result = 0;
        if (value.HasFlag(HotKeyModifiers.Alt)) result |= 0x0001;
        if (value.HasFlag(HotKeyModifiers.Control)) result |= 0x0002;
        if (value.HasFlag(HotKeyModifiers.Shift)) result |= 0x0004;
        return result;
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
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? moduleName);
}
