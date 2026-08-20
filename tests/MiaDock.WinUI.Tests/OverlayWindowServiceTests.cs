namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class OverlayWindowServiceTests
{
    [TestMethod]
    public void ThemeChange_RecreatesOnlyTheDockWindow()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Services",
            "OverlayWindowService.cs"));

        StringAssert.Contains(source, "_settings.SettingsChanged += OnSettingsChanged");
        StringAssert.Contains(source, "args.Previous.Appearance.Theme == args.Current.Appearance.Theme");
        StringAssert.Contains(source, "_dispatcher.TryEnqueue(RestartAfterThemeChange)");
        StringAssert.Contains(source, "previous.Close();");
        StringAssert.Contains(source, "var replacement = Current;");
        StringAssert.Contains(source, "replacement.ShowNoActivate();");
        Assert.DoesNotContain("IApplicationLifetimeService", source, StringComparison.Ordinal);
    }
}
