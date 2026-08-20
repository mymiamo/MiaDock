using System.ComponentModel;
using System.Runtime.InteropServices;
using MiaDock.Core.Logging;
using MiaDock.Core.Settings;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace MiaDock.Platform.Windows.HotKeys;

public sealed class WindowsGlobalHotKeyService : IGlobalHotKeyService
{
    private const uint WmHotKey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private static readonly nint HwndMessage = new(-3);

    private readonly WindowProcedure _windowProcedure;
    private readonly string _windowClassName = $"MiaDock.HotKeys.{Guid.NewGuid():N}";
    private readonly Dictionary<int, HotKeyAction> _registrations = [];
    private readonly Func<nint, int, uint, uint, bool> _registerHotKey;
    private readonly Func<nint, int, bool> _unregisterHotKey;
    private readonly ILogService? _log;
    private readonly bool _ownsNativeWindow;
    private nint _instance;
    private nint _windowHandle;
    private bool _initialized;
    private bool _disposed;

    public WindowsGlobalHotKeyService(ILogService? log = null)
    {
        _windowProcedure = HandleWindowMessage;
        _registerHotKey = static (window, id, modifiers, key) =>
            PInvoke.RegisterHotKey(new HWND(window), id, (HOT_KEY_MODIFIERS)modifiers, key);
        _unregisterHotKey = static (window, id) => PInvoke.UnregisterHotKey(new HWND(window), id);
        _log = log;
        _ownsNativeWindow = true;
    }

    internal WindowsGlobalHotKeyService(
        Func<nint, int, uint, uint, bool> registerHotKey,
        Func<nint, int, bool> unregisterHotKey,
        ILogService? log = null)
    {
        _windowProcedure = HandleWindowMessage;
        _registerHotKey = registerHotKey;
        _unregisterHotKey = unregisterHotKey;
        _log = log;
        _windowHandle = 1;
        _initialized = true;
        _ownsNativeWindow = false;
    }

    public event EventHandler<HotKeyAction>? Invoked;
    public event EventHandler? RegistrationsChanged;

    public IReadOnlyDictionary<HotKeyAction, HotKeyRegistrationStatus> RegistrationStatuses { get; private set; } =
        new Dictionary<HotKeyAction, HotKeyRegistrationStatus>();

    public IReadOnlyDictionary<HotKeyAction, HotKeyRegistrationStatus> Apply(GlobalHotKeySettings settings)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(settings);
        ClearRegistrations();

        var result = new Dictionary<HotKeyAction, HotKeyRegistrationStatus>();
        var validBindings = new List<(HotKeyAction Action, HotKeyGestureSetting Gesture)>();
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

            validBindings.Add((action, gesture));
        }

        if (validBindings.Count == 0)
        {
            return PublishStatuses(result);
        }

        try
        {
            EnsureInitialized();
        }
        catch (Exception exception)
        {
            foreach (var binding in validBindings)
            {
                result[binding.Action] = HotKeyRegistrationStatus.Conflict;
            }
            LogRegistrationFailure(exception, action: null, "initialize");
            return PublishStatuses(result);
        }

        foreach (var (action, gesture) in validBindings)
        {
            var id = 0x5100 + (int)action;
            var modifiers = ToNativeModifiers(gesture.Modifiers) | ModNoRepeat;
            try
            {
                if (!_registerHotKey(_windowHandle, id, modifiers, checked((uint)gesture.VirtualKey)))
                {
                    result[action] = HotKeyRegistrationStatus.Conflict;
                    LogRegistrationFailure(exception: null, action, "register");
                    continue;
                }
            }
            catch (Exception exception)
            {
                result[action] = HotKeyRegistrationStatus.Conflict;
                LogRegistrationFailure(exception, action, "register");
                continue;
            }

            _registrations[id] = action;
            result[action] = HotKeyRegistrationStatus.Registered;
        }

        return PublishStatuses(result);
    }

    public void Dispose()
    {
        if (_disposed) return;

        // Skip DestroyWindow/UnregisterClass: nested WndProc during teardown can
        // fail-fast as STATUS_STACK_BUFFER_OVERRUN. Hotkeys are cleared first;
        // process exit reclaims the message-only HWND.
        _disposed = true;
        ClearRegistrations();
        if (_ownsNativeWindow)
        {
            _windowHandle = 0;
        }
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
            try
            {
                _unregisterHotKey(_windowHandle, id);
            }
            catch (Exception exception)
            {
                LogRegistrationFailure(exception, _registrations[id], "unregister");
            }
        }
        _registrations.Clear();
    }

    private IReadOnlyDictionary<HotKeyAction, HotKeyRegistrationStatus> PublishStatuses(
        Dictionary<HotKeyAction, HotKeyRegistrationStatus> statuses)
    {
        RegistrationStatuses = statuses;
        RegistrationsChanged?.Invoke(this, EventArgs.Empty);
        return statuses;
    }

    private void LogRegistrationFailure(
        Exception? exception,
        HotKeyAction? action,
        string operation) =>
        _log?.Write(
            TechnicalLogLevel.Warning,
            TechnicalEventIds.HotKeyRegistrationFailed,
            "HotKeys",
            "A global hotkey registration operation failed.",
            exception,
            new Dictionary<string, object?>
            {
                ["action"] = action?.ToString(),
                ["operation"] = operation,
                ["win32Error"] = exception is null ? Marshal.GetLastWin32Error() : null
            });

    private nint HandleWindowMessage(nint window, uint message, nuint wParam, nint lParam)
    {
        try
        {
            if (!_disposed &&
                message == WmHotKey &&
                _registrations.TryGetValue(checked((int)wParam), out var action))
            {
                Invoked?.Invoke(this, action);
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
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClassW(string className, nint instance);
    [DllImport("user32.dll")]
    private static extern nint DefWindowProcW(nint window, uint message, nuint wParam, nint lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? moduleName);
}
