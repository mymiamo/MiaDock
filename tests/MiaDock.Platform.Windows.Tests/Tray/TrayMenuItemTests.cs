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
    public void MenuItems_CarrySemanticFluentIconKeys()
    {
        var item = new TrayMenuItem(
            42,
            "Settings",
            IconKey: TrayIconKey.Settings);

        Assert.AreEqual(TrayIconKey.Settings, item.IconKey);
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
        var resolverSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Tray",
            "FluentTrayIconResolver.cs"));

        StringAssert.Contains(source, "WinUIEx");
        StringAssert.Contains(source, "TrayIcon");
        StringAssert.Contains(source, "MenuFlyout");
        StringAssert.Contains(source, "MenuFlyoutSubItem");
        StringAssert.Contains(source, "ToggleMenuFlyoutItem");
        StringAssert.Contains(source, "FluentTrayIconResolver.Create");
        StringAssert.Contains(resolverSource, "SvgImageSource");
        Assert.DoesNotContain("IconGlyph", source, StringComparison.Ordinal);
        Assert.DoesNotContain("OwnerDrawMenuItem", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TrayNative", source, StringComparison.Ordinal);
    }

    [TestMethod]
    public void EverySemanticTrayIcon_ResolvesToAnIncludedFluentAsset()
    {
        var assetsDirectory = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "MiaDock.App",
            "Assets",
            "FluentIcons");
        foreach (var key in Enum.GetValues<TrayIconKey>().Where(key => key != TrayIconKey.None))
        {
            var asset = FluentTrayIconResolver.GetAssetName(key);
            Assert.IsFalse(string.IsNullOrWhiteSpace(asset), $"{key} has no asset mapping.");
            var assetPath = Path.Combine(assetsDirectory, asset);
            Assert.IsTrue(File.Exists(assetPath), $"{key} asset is missing: {asset}");
            var svg = File.ReadAllText(assetPath);
            StringAssert.Contains(svg, "#F5F5F5", $"{key} must keep contrast on the dark tray flyout.");
            Assert.DoesNotContain("#212121", svg, StringComparison.Ordinal);
        }
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
        StringAssert.Contains(source, "InvokeCommand(command)");
        StringAssert.Contains(source, "if (_disposed) return;");
        StringAssert.Contains(source, "CommandInvoked?.Invoke");
        StringAssert.Contains(source, "_icon.Dispose()");
        StringAssert.Contains(source, "_disposed = true;");
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "MiaDock.sln")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("MiaDock repository root was not found.");
    }
}
