namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class OverlayWindowServiceTests
{
    [TestMethod]
    public void ThemeChange_RefreshesTheDockWithoutDestroyingSharedNativeServices()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Services",
            "OverlayWindowService.cs"));

        StringAssert.Contains(source, "_settings.SettingsChanged += OnSettingsChanged");
        StringAssert.Contains(source, "args.Previous.Appearance.Theme == args.Current.Appearance.Theme");
        StringAssert.Contains(source, "_dispatcher.TryEnqueue(RefreshAfterThemeChange)");
        StringAssert.Contains(source, "_window.RefreshForThemeChange();");
        Assert.DoesNotContain("previous.Close();", source, StringComparison.Ordinal);
        Assert.DoesNotContain("var replacement = Current;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IApplicationLifetimeService", source, StringComparison.Ordinal);
    }
}
