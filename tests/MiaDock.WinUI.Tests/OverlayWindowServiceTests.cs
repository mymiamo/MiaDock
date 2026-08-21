namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class OverlayWindowServiceTests
{
    [TestMethod]
    public void OverlayService_UsesOneStableDockWindowForTheApplicationLifetime()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Services",
            "OverlayWindowService.cs"));

        StringAssert.Contains(source, "public OverlayWindow Current => _window ??= CreateWindow();");
        StringAssert.Contains(source, "window?.Close();");
        Assert.DoesNotContain("SettingsChanged", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshForThemeChange", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IApplicationLifetimeService", source, StringComparison.Ordinal);
    }
}
