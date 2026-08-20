using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinUIEx;

namespace MiaDock.Platform.Windows.Tray;

/// <summary>
/// A WinUIEx-backed tray icon.  It deliberately has no custom HWND or owner-draw
/// menu: WinUI owns menu focus, keyboard navigation, dismissal and Explorer recovery.
/// </summary>
public sealed class WindowsTrayIconService : ITrayIconService
{
    private const uint IconId = 1;
    private readonly string? _iconPath;
    private IReadOnlyList<TrayMenuItem> _items = [];
    private TrayIcon? _icon;
    private bool _disposed;

    public WindowsTrayIconService(string? iconPath = null) => _iconPath = iconPath;

    public bool IsVisible => _icon?.IsVisible == true;

    public event EventHandler<int>? CommandInvoked;
    public event EventHandler? PrimaryInvoked;

    public void Initialize(string toolTip)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_icon is not null) return;
        _icon = new TrayIcon(IconId, _iconPath ?? string.Empty, string.IsNullOrWhiteSpace(toolTip) ? "MiaDock" : toolTip.Trim());
        _icon.Selected += OnSelected;
        _icon.ContextMenu += OnContextMenu;
    }

    public void SetMenu(IReadOnlyList<TrayMenuItem> items)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _items = items?.ToArray() ?? throw new ArgumentNullException(nameof(items));
    }

    public void SetVisible(bool visible)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        EnsureInitialized();
        _icon!.IsVisible = visible;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_icon is null) return;
        _icon.Selected -= OnSelected;
        _icon.ContextMenu -= OnContextMenu;
        _icon.Dispose();
        _icon = null;
    }

    private void OnSelected(object? sender, EventArgs args)
    {
        if (_disposed) return;
        PrimaryInvoked?.Invoke(this, EventArgs.Empty);
    }

    private void OnContextMenu(object? sender, TrayIconEventArgs args)
    {
        args.Handled = true;
        if (_disposed) return;
        args.Flyout = CreateFlyout(_items);
    }

    private MenuFlyout CreateFlyout(IReadOnlyList<TrayMenuItem> items)
    {
        var flyout = new MenuFlyout();
        foreach (var item in items) flyout.Items.Add(CreateItem(item));
        return flyout;
    }

    private MenuFlyoutItemBase CreateItem(TrayMenuItem item)
    {
        if (item.IsSeparator) return new MenuFlyoutSeparator();
        if (item.Children is { Count: > 0 })
        {
            var submenu = new MenuFlyoutSubItem
            {
                Text = item.Text,
                IsEnabled = item.IsEnabled,
                Icon = FluentTrayIconResolver.Create(item.IconKey)
            };
            foreach (var child in item.Children) submenu.Items.Add(CreateItem(child));
            return submenu;
        }

        var command = item.CommandId;
        if (item.IsChecked || item.IsRadio)
        {
            var toggle = new ToggleMenuFlyoutItem
            {
                Text = item.Text,
                IsEnabled = item.IsEnabled,
                IsChecked = item.IsChecked,
                Icon = FluentTrayIconResolver.Create(item.IconKey)
            };
            toggle.Click += (_, _) => InvokeCommand(command);
            return toggle;
        }

        var menuItem = new MenuFlyoutItem
        {
            Text = item.Text,
            IsEnabled = item.IsEnabled,
            Icon = FluentTrayIconResolver.Create(item.IconKey)
        };
        menuItem.Click += (_, _) => InvokeCommand(command);
        return menuItem;
    }

    private void InvokeCommand(int command)
    {
        if (_disposed) return;
        CommandInvoked?.Invoke(this, command);
    }

    private void EnsureInitialized()
    {
        if (_icon is null) throw new InvalidOperationException("The tray icon service must be initialized first.");
    }

}
