using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MiaDock.Platform.Windows.Tray;

public sealed class WindowsTrayIconService : ITrayIconService
{
    private const uint IconId = 1;
    private const string CheckGlyph = "\uE73E";
    private const string RadioGlyph = "\uECCC";
    private const string ChevronGlyph = "\uE76C";

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
    private int _primaryInvokeQueued;
    private nuint _nextOwnerDrawId;
    private nint _menuBackgroundBrush;
    private nint _menuSelectionBrush;
    private nint _menuSeparatorBrush;
    private nint _menuTextFont;
    private nint _menuIconFont;
    private MenuChrome _chrome = MenuChrome.Dark(1.0);

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
        RefreshMenuChrome();
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

        // Mark the service disposed before destroying its native window. DestroyWindow
        // pumps teardown messages synchronously and must not be able to re-enter events.
        _disposed = true;
        Interlocked.Exchange(ref _primaryInvokeQueued, 0);

        try
        {
            if (IsVisible)
            {
                DeleteIcon();
            }
        }
        catch (Exception)
        {
        }

        // Do not DestroyWindow/UnregisterClass during teardown. DestroyWindow pumps
        // nested messages into this WndProc on a reverse-P/Invoke stack and has
        // produced STATUS_STACK_BUFFER_OVERRUN fail-fast dialogs on tray Exit.
        // The process exit that follows reclaims the message-only HWND.
        _windowHandle = 0;

        if (_ownsIcon && _icon != 0)
        {
            try
            {
                _ = TrayNative.DestroyIcon(_icon);
            }
            catch (Exception)
            {
            }

            _icon = 0;
            _ownsIcon = false;
        }

        try
        {
            DisposeMenuChrome();
        }
        catch (Exception)
        {
        }
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
        try
        {
            return WindowProcedureCore(windowHandle, message, wParam, lParam);
        }
        catch (Exception)
        {
            // A managed exception must never cross the reverse P/Invoke WndProc
            // boundary. Escaping here terminates the process through a native
            // fail-fast path before AppExceptionCoordinator can record it.
            return TrayNative.DefWindowProcW(windowHandle, message, wParam, lParam);
        }
    }

    private nint WindowProcedureCore(nint windowHandle, uint message, nuint wParam, nint lParam)
    {
        if (_disposed)
        {
            return TrayNative.DefWindowProcW(windowHandle, message, wParam, lParam);
        }

        if (message == TrayNative.TrayDispatchMessage)
        {
            if (wParam == 0)
            {
                Interlocked.Exchange(ref _primaryInvokeQueued, 0);
                PrimaryInvoked?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                CommandInvoked?.Invoke(this, checked((int)wParam));
            }

            return 0;
        }

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

            if (notification is TrayNative.WmLButtonUp or TrayNative.NinSelect)
            {
                QueuePrimaryInvoke();
                return 0;
            }

            if (notification == TrayNative.WmLButtonDoubleClick)
            {
                // The preceding button-up already scheduled the configured primary action.
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
        RefreshMenuChrome();
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
                // Never open/activate WinUI windows from inside the tray callback stack.
                // Posting a private message lets TrackPopupMenuEx and the shell callback
                // unwind before application code is invoked on the same UI thread.
                _ = TrayNative.PostMessageW(
                    _windowHandle,
                    TrayNative.TrayDispatchMessage,
                    command,
                    0);
            }
        }
        finally
        {
            _ = TrayNative.PostMessageW(_windowHandle, TrayNative.WmNull, 0, 0);
            TrayNative.DestroyMenu(menu);
            _ownerDrawItems.Clear();
        }
    }

    private void QueuePrimaryInvoke()
    {
        if (Interlocked.Exchange(ref _primaryInvokeQueued, 1) != 0)
        {
            return;
        }

        if (!TrayNative.PostMessageW(
                _windowHandle,
                TrayNative.TrayDispatchMessage,
                0,
                0))
        {
            Interlocked.Exchange(ref _primaryInvokeQueued, 0);
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

        var reserveIconColumn = items.Any(static item =>
            !item.IsSeparator && !string.IsNullOrWhiteSpace(item.IconGlyph));

        // InsertMenuItem + MIIM_SUBMENU is required for owner-draw parents.
        // AppendMenu(MF_POPUP | MF_OWNERDRAW) can drop the submenu association,
        // which leaves the chevron visible but the flyout never opens.
        uint position = 0;
        foreach (var item in items)
        {
            var itemData = ++_nextOwnerDrawId;
            var hasChildren = item.Children is { Count: > 0 };
            _ownerDrawItems[itemData] = new OwnerDrawMenuItem(
                item.Text,
                item.IsSeparator,
                item.IsEnabled,
                item.IsChecked,
                item.IsRadio,
                hasChildren,
                item.IconGlyph,
                reserveIconColumn);

            var info = new TrayNative.MenuItemInfo
            {
                Size = (uint)Marshal.SizeOf<TrayNative.MenuItemInfo>(),
                Mask = TrayNative.MiimFtype | TrayNative.MiimState | TrayNative.MiimData,
                Type = item.IsSeparator
                    ? TrayNative.MftSeparator | TrayNative.MftOwnerDraw
                    : TrayNative.MftOwnerDraw,
                State = 0,
                ItemData = itemData
            };

            if (!item.IsSeparator)
            {
                if (!item.IsEnabled)
                {
                    info.State |= TrayNative.MfsDisabled;
                }

                if (item.IsChecked)
                {
                    info.State |= TrayNative.MfsChecked;
                }

                if (hasChildren)
                {
                    var childMenu = CreateMenu(item.Children!);
                    info.Mask |= TrayNative.MiimSubmenu;
                    info.SubMenu = childMenu;
                }
                else
                {
                    info.Mask |= TrayNative.MiimId;
                    info.Id = checked((uint)item.CommandId);
                }
            }

            if (!TrayNative.InsertMenuItemW(menu, position, true, ref info))
            {
                // Fall back keeps the rest of the menu usable if one row fails.
                continue;
            }

            position++;
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

        if (item.IsSeparator)
        {
            measure.ItemHeight = (uint)_chrome.SeparatorHeight;
            measure.ItemWidth = (uint)_chrome.MinItemWidth;
            Marshal.StructureToPtr(measure, parameter, false);
            return true;
        }

        var textWidth = MeasureTextWidth(item.Text, _menuTextFont);
        var width = _chrome.ContentLeft
                    + _chrome.MarkColumnWidth
                    + (item.ReserveIconColumn ? _chrome.IconColumnWidth : 0)
                    + textWidth
                    + _chrome.TextTrailingPadding
                    + (item.HasChildren ? _chrome.ChevronColumnWidth : _chrome.EdgePadding)
                    + _chrome.EdgePadding;
        measure.ItemHeight = (uint)_chrome.ItemHeight;
        measure.ItemWidth = (uint)Math.Clamp(width, _chrome.MinItemWidth, _chrome.MaxItemWidth);
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
        var bounds = draw.ItemRectangle;
        _ = TrayNative.FillRect(draw.DeviceContext, ref bounds, _menuBackgroundBrush);

        if (item.IsSeparator)
        {
            var separator = new TrayNative.Rect
            {
                Left = bounds.Left + _chrome.SeparatorInset,
                Top = (bounds.Top + bounds.Bottom) / 2,
                Right = bounds.Right - _chrome.SeparatorInset,
                Bottom = (bounds.Top + bounds.Bottom) / 2 + 1
            };
            _ = TrayNative.FillRect(
                draw.DeviceContext,
                ref separator,
                _menuSeparatorBrush);
            return true;
        }

        if (selected && !disabled)
        {
            var highlight = new TrayNative.Rect
            {
                Left = bounds.Left + _chrome.SelectionInset,
                Top = bounds.Top + _chrome.SelectionInset,
                Right = bounds.Right - _chrome.SelectionInset,
                Bottom = bounds.Bottom - _chrome.SelectionInset
            };
            _ = TrayNative.FillRect(draw.DeviceContext, ref highlight, _menuSelectionBrush);
        }

        _ = TrayNative.SetBkMode(draw.DeviceContext, TrayNative.Transparent);
        var textColor = disabled ? _chrome.DisabledTextColor : _chrome.TextColor;
        _ = TrayNative.SetTextColor(draw.DeviceContext, textColor);

        var cursor = bounds.Left + _chrome.ContentLeft;
        var markBounds = new TrayNative.Rect
        {
            Left = cursor,
            Top = bounds.Top,
            Right = cursor + _chrome.MarkColumnWidth,
            Bottom = bounds.Bottom
        };
        if (item.IsChecked)
        {
            _ = TrayNative.SetTextColor(draw.DeviceContext, _chrome.AccentColor);
            DrawText(
                draw.DeviceContext,
                item.IsRadio ? RadioGlyph : CheckGlyph,
                ref markBounds,
                _menuIconFont,
                TrayNative.DtCenter);
            _ = TrayNative.SetTextColor(draw.DeviceContext, textColor);
        }

        cursor += _chrome.MarkColumnWidth;
        if (item.ReserveIconColumn)
        {
            if (!string.IsNullOrWhiteSpace(item.IconGlyph))
            {
                var iconBounds = new TrayNative.Rect
                {
                    Left = cursor,
                    Top = bounds.Top,
                    Right = cursor + _chrome.IconColumnWidth,
                    Bottom = bounds.Bottom
                };
                DrawText(
                    draw.DeviceContext,
                    item.IconGlyph,
                    ref iconBounds,
                    _menuIconFont,
                    TrayNative.DtCenter);
            }

            cursor += _chrome.IconColumnWidth;
        }

        var textRight = bounds.Right
                        - (item.HasChildren ? _chrome.ChevronColumnWidth : _chrome.EdgePadding);
        var textBounds = new TrayNative.Rect
        {
            Left = cursor,
            Top = bounds.Top,
            Right = textRight,
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
                Left = bounds.Right - _chrome.ChevronColumnWidth,
                Top = bounds.Top,
                Right = bounds.Right - _chrome.EdgePadding,
                Bottom = bounds.Bottom
            };
            DrawText(
                draw.DeviceContext,
                ChevronGlyph,
                ref arrowBounds,
                _menuIconFont,
                TrayNative.DtCenter);
        }

        return true;
    }

    private int MeasureTextWidth(string text, nint font)
    {
        if (string.IsNullOrEmpty(text) || font == 0)
        {
            return 0;
        }

        var deviceContext = TrayNative.GetDC(0);
        if (deviceContext == 0)
        {
            return text.Length * _chrome.FallbackCharWidth;
        }

        try
        {
            var previous = TrayNative.SelectObject(deviceContext, font);
            if (!TrayNative.GetTextExtentPoint32W(deviceContext, text, text.Length, out var size))
            {
                return text.Length * _chrome.FallbackCharWidth;
            }

            if (previous != 0)
            {
                _ = TrayNative.SelectObject(deviceContext, previous);
            }

            return Math.Max(0, size.Width);
        }
        finally
        {
            _ = TrayNative.ReleaseDC(0, deviceContext);
        }
    }

    private void RefreshMenuChrome()
    {
        var scale = ResolveScale();
        var light = IsAppsUseLightTheme();
        _chrome = light ? MenuChrome.Light(scale) : MenuChrome.Dark(scale);

        DisposeMenuChrome();
        _menuBackgroundBrush = TrayNative.CreateSolidBrush(_chrome.BackgroundColor);
        _menuSelectionBrush = TrayNative.CreateSolidBrush(_chrome.SelectionColor);
        _menuSeparatorBrush = TrayNative.CreateSolidBrush(_chrome.SeparatorColor);
        _menuTextFont = CreateMenuFont("Segoe UI", _chrome.TextFontPixels, 400);
        _menuIconFont = CreateMenuFont("Segoe Fluent Icons", _chrome.IconFontPixels, 400);
    }

    private void DisposeMenuChrome()
    {
        DeleteGdiObject(ref _menuBackgroundBrush);
        DeleteGdiObject(ref _menuSelectionBrush);
        DeleteGdiObject(ref _menuSeparatorBrush);
        DeleteGdiObject(ref _menuTextFont);
        DeleteGdiObject(ref _menuIconFont);
    }

    private double ResolveScale()
    {
        var dpi = _windowHandle != 0
            ? TrayNative.GetDpiForWindow(_windowHandle)
            : 0u;
        if (dpi == 0)
        {
            dpi = TrayNative.GetDpiForSystem();
        }

        if (dpi == 0)
        {
            dpi = 96;
        }

        return dpi / 96.0;
    }

    private static bool IsAppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int light && light != 0;
        }
        catch
        {
            return false;
        }
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

    private sealed record OwnerDrawMenuItem(
        string Text,
        bool IsSeparator,
        bool IsEnabled,
        bool IsChecked,
        bool IsRadio,
        bool HasChildren,
        string? IconGlyph,
        bool ReserveIconColumn);

    private sealed record MenuChrome(
        uint BackgroundColor,
        uint SelectionColor,
        uint SeparatorColor,
        uint TextColor,
        uint DisabledTextColor,
        uint AccentColor,
        int ItemHeight,
        int SeparatorHeight,
        int MinItemWidth,
        int MaxItemWidth,
        int ContentLeft,
        int MarkColumnWidth,
        int IconColumnWidth,
        int ChevronColumnWidth,
        int EdgePadding,
        int TextTrailingPadding,
        int SelectionInset,
        int SeparatorInset,
        int TextFontPixels,
        int IconFontPixels,
        int FallbackCharWidth)
    {
        public static MenuChrome Dark(double scale) => Create(
            scale,
            background: ColorRef(45, 51, 61),
            selection: ColorRef(62, 72, 86),
            separator: ColorRef(83, 91, 104),
            text: ColorRef(246, 247, 249),
            disabled: ColorRef(145, 151, 161),
            accent: ColorRef(99, 158, 255));

        public static MenuChrome Light(double scale) => Create(
            scale,
            background: ColorRef(249, 249, 249),
            selection: ColorRef(232, 232, 232),
            separator: ColorRef(200, 200, 200),
            text: ColorRef(28, 28, 28),
            disabled: ColorRef(140, 140, 140),
            accent: ColorRef(0, 103, 192));

        private static MenuChrome Create(
            double scale,
            uint background,
            uint selection,
            uint separator,
            uint text,
            uint disabled,
            uint accent)
        {
            var safeScale = Math.Clamp(scale, 1.0, 3.0);
            int Scaled(int value) => Math.Max(1, (int)Math.Round(value * safeScale));

            return new MenuChrome(
                background,
                selection,
                separator,
                text,
                disabled,
                accent,
                ItemHeight: Scaled(36),
                SeparatorHeight: Scaled(9),
                MinItemWidth: Scaled(260),
                MaxItemWidth: Scaled(440),
                ContentLeft: Scaled(8),
                MarkColumnWidth: Scaled(22),
                IconColumnWidth: Scaled(28),
                ChevronColumnWidth: Scaled(24),
                EdgePadding: Scaled(8),
                TextTrailingPadding: Scaled(12),
                SelectionInset: Scaled(4),
                SeparatorInset: Scaled(14),
                TextFontPixels: Scaled(14),
                IconFontPixels: Scaled(16),
                FallbackCharWidth: Scaled(7));
        }

        private static uint ColorRef(byte red, byte green, byte blue) =>
            red | (uint)green << 8 | (uint)blue << 16;
    }
}
