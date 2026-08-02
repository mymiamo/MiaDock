using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MiaDock.Platform.Windows.Tray;

public sealed class WindowsTrayIconService : ITrayIconService
{
    private const uint IconId = 1;
    private readonly TrayNative.WindowProcedure _windowProcedure;
    private readonly string? _iconPath;
    private readonly string _windowClassName = $"MiaDock.Tray.{Guid.NewGuid():N}";
    private readonly Dictionary<nuint, OwnerDrawMenuItem> _ownerDrawItems = new();
    private IReadOnlyList<TrayMenuItem> _items = Array.Empty<TrayMenuItem>();
    private nint _instance;
    private nint _windowHandle;
    private nint _icon;
    private uint _taskbarCreatedMessage;
    private string _toolTip = "MiaDock";
    private bool _initialized;
    private bool _disposed;
    private bool _ownsIcon;
    private nuint _nextOwnerDrawId;
    private nint _menuBackgroundBrush;
    private nint _menuSelectionBrush;
    private nint _menuSeparatorBrush;
    private nint _menuTextFont;
    private nint _menuIconFont;

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
        _menuBackgroundBrush = TrayNative.CreateSolidBrush(ColorRef(45, 51, 61));
        _menuSelectionBrush = TrayNative.CreateSolidBrush(ColorRef(62, 72, 86));
        _menuSeparatorBrush = TrayNative.CreateSolidBrush(ColorRef(83, 91, 104));
        _menuTextFont = CreateMenuFont("Segoe UI", 15, 400);
        _menuIconFont = CreateMenuFont("Segoe Fluent Icons", 16, 400);
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
        DeleteGdiObject(ref _menuBackgroundBrush);
        DeleteGdiObject(ref _menuSelectionBrush);
        DeleteGdiObject(ref _menuSeparatorBrush);
        DeleteGdiObject(ref _menuTextFont);
        DeleteGdiObject(ref _menuIconFont);

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

        if (message == TrayNative.WmMeasureItem &&
            TryMeasureMenuItem(lParam))
        {
            return 1;
        }

        if (message == TrayNative.WmDrawItem &&
            TryDrawMenuItem(lParam))
        {
            return 1;
        }

        return TrayNative.DefWindowProcW(windowHandle, message, wParam, lParam);
    }

    private void ShowMenu()
    {
        _ownerDrawItems.Clear();
        _nextOwnerDrawId = 0;
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
            _ownerDrawItems.Clear();
        }
    }

    private nint CreateMenu(IReadOnlyList<TrayMenuItem> items)
    {
        var menu = TrayNative.CreatePopupMenu();
        if (menu == 0)
        {
            return 0;
        }

        var menuInfo = new TrayNative.MenuInfo
        {
            Size = (uint)Marshal.SizeOf<TrayNative.MenuInfo>(),
            Mask = TrayNative.MimBackground,
            BackgroundBrush = _menuBackgroundBrush
        };
        _ = TrayNative.SetMenuInfo(menu, ref menuInfo);

        foreach (var item in items)
        {
            var itemData = ++_nextOwnerDrawId;
            _ownerDrawItems[itemData] = new OwnerDrawMenuItem(
                item.Text,
                item.IsSeparator,
                item.IsEnabled,
                item.IsChecked,
                item.Children is { Count: > 0 },
                item.IconGlyph);
            if (item.IsSeparator)
            {
                _ = TrayNative.AppendOwnerDrawMenuW(
                    menu,
                    TrayNative.MfSeparator | TrayNative.MfOwnerDraw,
                    0,
                    checked((nint)itemData));
                continue;
            }

            var flags = TrayNative.MfOwnerDraw;
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
                _ = TrayNative.AppendOwnerDrawMenuW(
                    menu,
                    flags,
                    checked((nuint)childMenu),
                    checked((nint)itemData));
            }
            else
            {
                _ = TrayNative.AppendOwnerDrawMenuW(
                    menu,
                    flags,
                    checked((nuint)item.CommandId),
                    checked((nint)itemData));
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

    private bool TryMeasureMenuItem(nint parameter)
    {
        var measure = Marshal.PtrToStructure<TrayNative.MeasureItem>(parameter);
        if (measure.ControlType != TrayNative.OdtMenu ||
            !_ownerDrawItems.TryGetValue(measure.ItemData, out var item))
        {
            return false;
        }

        measure.ItemHeight = item.IsSeparator ? 9u : 34u;
        measure.ItemWidth = item.IsSeparator
            ? 260u
            : checked((uint)Math.Clamp(116 + item.Text.Length * 7, 260, 420));
        Marshal.StructureToPtr(measure, parameter, false);
        return true;
    }

    private bool TryDrawMenuItem(nint parameter)
    {
        var draw = Marshal.PtrToStructure<TrayNative.DrawItem>(parameter);
        if (draw.ControlType != TrayNative.OdtMenu ||
            !_ownerDrawItems.TryGetValue(draw.ItemData, out var item))
        {
            return false;
        }

        var selected = (draw.ItemState & TrayNative.OdsSelected) != 0;
        var disabled = !item.IsEnabled ||
                       (draw.ItemState &
                        (TrayNative.OdsDisabled | TrayNative.OdsGrayed)) != 0;
        var background = selected && !disabled
            ? _menuSelectionBrush
            : _menuBackgroundBrush;
        var bounds = draw.ItemRectangle;
        _ = TrayNative.FillRect(draw.DeviceContext, ref bounds, background);

        if (item.IsSeparator)
        {
            var separator = new TrayNative.Rect
            {
                Left = bounds.Left + 12,
                Top = (bounds.Top + bounds.Bottom) / 2,
                Right = bounds.Right - 12,
                Bottom = (bounds.Top + bounds.Bottom) / 2 + 1
            };
            _ = TrayNative.FillRect(
                draw.DeviceContext,
                ref separator,
                _menuSeparatorBrush);
            return true;
        }

        _ = TrayNative.SetBkMode(
            draw.DeviceContext,
            TrayNative.Transparent);
        _ = TrayNative.SetTextColor(
            draw.DeviceContext,
            disabled
                ? ColorRef(145, 151, 161)
                : ColorRef(246, 247, 249));

        if (!string.IsNullOrWhiteSpace(item.IconGlyph))
        {
            var iconBounds = new TrayNative.Rect
            {
                Left = bounds.Left + 10,
                Top = bounds.Top,
                Right = bounds.Left + 38,
                Bottom = bounds.Bottom
            };
            DrawText(
                draw.DeviceContext,
                item.IconGlyph,
                ref iconBounds,
                _menuIconFont,
                TrayNative.DtCenter);
        }
        else if (item.IsChecked)
        {
            var checkBounds = new TrayNative.Rect
            {
                Left = bounds.Left + 10,
                Top = bounds.Top,
                Right = bounds.Left + 38,
                Bottom = bounds.Bottom
            };
            _ = TrayNative.SetTextColor(
                draw.DeviceContext,
                ColorRef(99, 158, 255));
            DrawText(
                draw.DeviceContext,
                "\uE73E",
                ref checkBounds,
                _menuIconFont,
                TrayNative.DtCenter);
            _ = TrayNative.SetTextColor(
                draw.DeviceContext,
                disabled
                    ? ColorRef(145, 151, 161)
                    : ColorRef(246, 247, 249));
        }

        var textBounds = new TrayNative.Rect
        {
            Left = bounds.Left + 42,
            Top = bounds.Top,
            Right = bounds.Right - (item.HasChildren ? 30 : 12),
            Bottom = bounds.Bottom
        };
        DrawText(
            draw.DeviceContext,
            item.Text,
            ref textBounds,
            _menuTextFont,
            0);

        if (item.HasChildren)
        {
            var arrowBounds = new TrayNative.Rect
            {
                Left = bounds.Right - 30,
                Top = bounds.Top,
                Right = bounds.Right - 8,
                Bottom = bounds.Bottom
            };
            DrawText(
                draw.DeviceContext,
                "\uE76C",
                ref arrowBounds,
                _menuIconFont,
                TrayNative.DtCenter);
        }

        return true;
    }

    private static void DrawText(
        nint deviceContext,
        string text,
        ref TrayNative.Rect rectangle,
        nint font,
        int alignment)
    {
        var previousFont = font != 0
            ? TrayNative.SelectObject(deviceContext, font)
            : 0;
        _ = TrayNative.DrawTextW(
            deviceContext,
            text,
            text.Length,
            ref rectangle,
            alignment |
            TrayNative.DtVCenter |
            TrayNative.DtSingleLine |
            TrayNative.DtNoPrefix);
        if (previousFont != 0)
        {
            _ = TrayNative.SelectObject(deviceContext, previousFont);
        }
    }

    private static nint CreateMenuFont(
        string family,
        int pixelHeight,
        int weight) =>
        TrayNative.CreateFontW(
            -pixelHeight,
            0,
            0,
            0,
            weight,
            0,
            0,
            0,
            1,
            0,
            0,
            5,
            0,
            family);

    private static void DeleteGdiObject(ref nint handle)
    {
        if (handle == 0)
        {
            return;
        }

        _ = TrayNative.DeleteObject(handle);
        handle = 0;
    }

    private static uint ColorRef(byte red, byte green, byte blue) =>
        red | (uint)green << 8 | (uint)blue << 16;

    private sealed record OwnerDrawMenuItem(
        string Text,
        bool IsSeparator,
        bool IsEnabled,
        bool IsChecked,
        bool HasChildren,
        string? IconGlyph);
}
