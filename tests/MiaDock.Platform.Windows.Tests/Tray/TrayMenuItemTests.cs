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
    public void WindowsTrayMenu_UsesOwnerDrawnDarkSurface()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Tray",
            "WindowsTrayIconService.cs"));

        StringAssert.Contains(source, "OwnerDrawMenuItem");
        StringAssert.Contains(source, "TryMeasureMenuItem");
        StringAssert.Contains(source, "TryDrawMenuItem");
        StringAssert.Contains(source, "ColorRef(45, 51, 61)");
        StringAssert.Contains(source, "Segoe Fluent Icons");
    }
}
