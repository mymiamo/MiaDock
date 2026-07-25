using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MiaDock.Platform.Windows.Tray;

public sealed class WindowsTrayIconService : ITrayIconService
{
    private const uint IconId = 1;
    private readonly TrayNative.WindowProcedure _windowProcedure;
    private readonly string? _iconPath;
    private readonly string _windowClassName = $"MiaDock.Tray.{Guid.NewGuid():N}";
    private IReadOnlyList<TrayMenuItem> _items = Array.Empty<TrayMenuItem>();
    private nint _instance;
    private nint _windowHandle;
    private nint _icon;
    private uint _taskbarCreatedMessage;
    private string _toolTip = "MiaDock";
    private bool _initialized;
    private bool _disposed;
    private bool _ownsIcon;

    public WindowsTrayIconService(string? iconPath = null)
    {
        _iconPath = iconPath;
        _windowProcedure = WindowProcedure;
    }

    public bool IsVisible { get; private set; }

    public event EventHandler<int>? CommandInvoked;

    public event EventHandler? PrimaryInvoked;

    public void Initialize(string toolTip)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
        {
            return;
        }

        _toolTip = string.IsNullOrWhiteSpace(toolTip) ? "MiaDock" : toolTip.Trim();
        _instance = TrayNative.GetModuleHandleW(null);
        var windowClass = new TrayNative.WindowClass
        {
            Size = (uint)Marshal.SizeOf<TrayNative.WindowClass>(),
            Instance = _instance,
            WindowProcedure = _windowProcedure,
            ClassName = _windowClassName
        };
        if (TrayNative.RegisterClassExW(ref windowClass) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "The tray message window class could not be registered.");
        }

        _windowHandle = TrayNative.CreateWindowExW(
            0,
            _windowClassName,
            string.Empty,
            0,
            0,
            0,
            0,
            0,
            TrayNative.HwndMessage,
            0,
            _instance,
            0);
        if (_windowHandle == 0)
        {
            var error = Marshal.GetLastWin32Error();
            TrayNative.UnregisterClassW(_windowClassName, _instance);
            throw new Win32Exception(error, "The tray message window could not be created.");
        }

        if (!string.IsNullOrWhiteSpace(_iconPath) && File.Exists(_iconPath))
        {
            _icon = TrayNative.LoadImageW(
                0,
                _iconPath,
                TrayNative.ImageIcon,
                0,
                0,
                TrayNative.LrLoadFromFile | TrayNative.LrDefaultSize);
            _ownsIcon = _icon != 0;
        }
        if (_icon == 0)
        {
            _icon = TrayNative.LoadIconW(0, TrayNative.IdiApplication);
        }
        _taskbarCreatedMessage = TrayNative.RegisterWindowMessageW("TaskbarCreated");
        _initialized = true;
    }

    public void SetMenu(IReadOnlyList<TrayMenuItem> items)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(items);
        _items = items.ToArray();
    }

    public void SetVisible(bool visible)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureInitialized();
        if (visible == IsVisible)
        {
            return;
        }

        if (visible)
        {
            AddIcon();
        }
        else
        {
            DeleteIcon();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (IsVisible)
        {
            DeleteIcon();
        }

        if (_windowHandle != 0)
        {
            TrayNative.DestroyWindow(_windowHandle);
            _windowHandle = 0;
        }

        if (_instance != 0 && _initialized)
        {
            TrayNative.UnregisterClassW(_windowClassName, _instance);
        }
        if (_ownsIcon && _icon != 0)
        {
            _ = TrayNative.DestroyIcon(_icon);
            _icon = 0;
            _ownsIcon = false;
        }

        _disposed = true;
    }

    private void AddIcon()
    {
        var data = CreateIconData(TrayNative.NifMessage | TrayNative.NifIcon | TrayNative.NifTip);
        if (!TrayNative.Shell_NotifyIconW(TrayNative.NimAdd, ref data))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "The tray icon could not be added.");
        }

        data.TimeoutOrVersion = TrayNative.NotifyIconVersion4;
        _ = TrayNative.Shell_NotifyIconW(TrayNative.NimSetVersion, ref data);
        IsVisible = true;
    }

    private void DeleteIcon()
    {
        var data = CreateIconData(0);
        _ = TrayNative.Shell_NotifyIconW(TrayNative.NimDelete, ref data);
        IsVisible = false;
    }

    private TrayNative.NotifyIconData CreateIconData(uint flags) => new()
    {
        Size = (uint)Marshal.SizeOf<TrayNative.NotifyIconData>(),
        WindowHandle = _windowHandle,
        Id = IconId,
        Flags = flags,
        CallbackMessage = TrayNative.TrayCallbackMessage,
        Icon = _icon,
        ToolTip = _toolTip,
        Info = string.Empty,
        InfoTitle = string.Empty
    };

    private nint WindowProcedure(nint windowHandle, uint message, nuint wParam, nint lParam)
    {
        if (message == _taskbarCreatedMessage && _taskbarCreatedMessage != 0)
        {
            if (IsVisible)
            {
                IsVisible = false;
                AddIcon();
            }

            return 0;
        }

        if (message == TrayNative.TrayCallbackMessage)
        {
            var notification = (uint)((nuint)lParam & 0xFFFF);
            if (notification is TrayNative.WmContextMenu or TrayNative.WmRButtonUp)
            {
                ShowMenu();
                return 0;
            }

            if (notification == TrayNative.WmLButtonDoubleClick)
            {
                PrimaryInvoked?.Invoke(this, EventArgs.Empty);
                return 0;
            }
        }

        return TrayNative.DefWindowProcW(windowHandle, message, wParam, lParam);
    }

    private void ShowMenu()
    {
        var menu = CreateMenu(_items);
        if (menu == 0)
        {
            return;
        }

        try
        {
            _ = TrayNative.SetForegroundWindow(_windowHandle);
            if (!TrayNative.GetCursorPos(out var point))
            {
                return;
            }

            var command = TrayNative.TrackPopupMenuEx(
                menu,
                TrayNative.TpmRightButton | TrayNative.TpmReturnCommand,
                point.X,
                point.Y,
                _windowHandle,
                0);
            if (command != 0)
            {
                CommandInvoked?.Invoke(this, checked((int)command));
            }
        }
        finally
        {
            _ = TrayNative.PostMessageW(_windowHandle, TrayNative.WmNull, 0, 0);
            TrayNative.DestroyMenu(menu);
        }
    }

    private static nint CreateMenu(IReadOnlyList<TrayMenuItem> items)
    {
        var menu = TrayNative.CreatePopupMenu();
        if (menu == 0)
        {
            return 0;
        }

        foreach (var item in items)
        {
            if (item.IsSeparator)
            {
                _ = TrayNative.AppendMenuW(menu, TrayNative.MfSeparator, 0, null);
                continue;
            }

            var flags = TrayNative.MfString;
            if (!item.IsEnabled)
            {
                flags |= TrayNative.MfGray;
            }

            if (item.IsChecked)
            {
                flags |= TrayNative.MfChecked;
            }

            if (item.Children is { Count: > 0 })
            {
                var childMenu = CreateMenu(item.Children);
                flags |= TrayNative.MfPopup;
                _ = TrayNative.AppendMenuW(menu, flags, (nuint)childMenu, item.Text);
            }
            else
            {
                _ = TrayNative.AppendMenuW(menu, flags, checked((nuint)item.CommandId), item.Text);
            }
        }

        return menu;
    }

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("The tray icon service must be initialized first.");
        }
    }
}
