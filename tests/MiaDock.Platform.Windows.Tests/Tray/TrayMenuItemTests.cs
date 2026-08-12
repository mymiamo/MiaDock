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
    public void WindowsTrayMenu_UsesThemeAwareOwnerDrawnSurface()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Tray",
            "WindowsTrayIconService.cs"));

        StringAssert.Contains(source, "OwnerDrawMenuItem");
        StringAssert.Contains(source, "TryMeasureMenuItem");
        StringAssert.Contains(source, "TryDrawMenuItem");
        StringAssert.Contains(source, "RefreshMenuChrome");
        StringAssert.Contains(source, "AppsUseLightTheme");
        StringAssert.Contains(source, "GetDpiForWindow");
        StringAssert.Contains(source, "GetTextExtentPoint32W");
        StringAssert.Contains(source, "MarkColumnWidth");
        StringAssert.Contains(source, "IsRadio");
        StringAssert.Contains(source, "Segoe Fluent Icons");
        StringAssert.Contains(source, "MenuChrome.Light");
        StringAssert.Contains(source, "MenuChrome.Dark");
        StringAssert.Contains(source, "InsertMenuItemW");
        StringAssert.Contains(source, "MiimSubmenu");
        StringAssert.Contains(source, "Do not DestroyWindow/UnregisterClass during teardown");
    }

    [TestMethod]
    public void WindowsTrayCallbacks_DeferUiActionsUntilNativeCallbackUnwinds()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Tray",
            "WindowsTrayIconService.cs"));

        StringAssert.Contains(source, "TrayDispatchMessage");
        StringAssert.Contains(source, "QueuePrimaryInvoke");
        StringAssert.Contains(source, "PostMessageW");
        StringAssert.Contains(source, "WindowProcedureCore");
        StringAssert.Contains(source, "must never cross the reverse P/Invoke WndProc");
        StringAssert.Contains(source, "notification is TrayNative.WmLButtonUp or TrayNative.NinSelect");
        StringAssert.Contains(source, "_disposed = true;");
        Assert.IsFalse(source.Contains(
            "notification == TrayNative.WmLButtonDoubleClick)\r\n            {\r\n                PrimaryInvoked?.Invoke",
            StringComparison.Ordinal));
    }
}
