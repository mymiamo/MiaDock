using MiaDock.Platform.Windows.Tray;

namespace MiaDock.Platform.Windows.Tests.Tray;

[TestClass]
public sealed class TrayMenuItemTests
{
    [TestMethod]
    public void Separator_HasNoCommandOrText()
    {
        Assert.IsTrue(TrayMenuItem.Separator.IsSeparator);
        Assert.AreEqual(0, TrayMenuItem.Separator.CommandId);
        Assert.AreEqual(string.Empty, TrayMenuItem.Separator.Text);
    }

    [TestMethod]
    public void Submenu_PreservesCheckedAndDisabledChildren()
    {
        var children = new[]
        {
            new TrayMenuItem(1, "First", IsChecked: true),
            new TrayMenuItem(2, "Second", IsEnabled: false)
        };
        var menu = new TrayMenuItem(0, "Parent", Children: children);

        Assert.IsTrue(menu.Children![0].IsChecked);
        Assert.IsFalse(menu.Children[1].IsEnabled);
    }

    [TestMethod]
    public void MenuItems_CanCarryFluentIconGlyphs()
    {
        var item = new TrayMenuItem(
            42,
            "Settings",
            IconGlyph: "\uE713");

        Assert.AreEqual("\uE713", item.IconGlyph);
    }

    [TestMethod]
    public void MenuItems_CanMarkRadioSelections()
    {
        var item = new TrayMenuItem(
            7,
            "Primary monitor",
            IsChecked: true,
            IsRadio: true);

        Assert.IsTrue(item.IsRadio);
        Assert.IsTrue(item.IsChecked);
    }

    [TestMethod]
    public void WindowsTrayMenu_UsesWinUiExFlyoutInsteadOfNativeOwnerDraw()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Tray",
            "WindowsTrayIconService.cs"));

        StringAssert.Contains(source, "WinUIEx");
        StringAssert.Contains(source, "TrayIcon");
        StringAssert.Contains(source, "MenuFlyout");
        StringAssert.Contains(source, "MenuFlyoutSubItem");
        StringAssert.Contains(source, "ToggleMenuFlyoutItem");
        StringAssert.Contains(source, "CreateFluentIcon");
        StringAssert.Contains(source, "SvgImageSource");
        Assert.DoesNotContain("OwnerDrawMenuItem", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TrayNative", source, StringComparison.Ordinal);
    }

    [TestMethod]
    public void WindowsTrayCallbacks_UseWinUiExEventsAndDisposeSafely()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Tray",
            "WindowsTrayIconService.cs"));

        StringAssert.Contains(source, "_icon.Selected += OnSelected");
        StringAssert.Contains(source, "_icon.ContextMenu += OnContextMenu");
        StringAssert.Contains(source, "args.Flyout = CreateFlyout(_items)");
        StringAssert.Contains(source, "CommandInvoked?.Invoke");
        StringAssert.Contains(source, "_icon.Dispose()");
        StringAssert.Contains(source, "_disposed = true;");
    }
}
