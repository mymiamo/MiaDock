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
}
