namespace MiaDock.Platform.Windows.Tests.Applications;

[TestClass]
public sealed class WindowsFocusSettingsLauncherTests
{
    [TestMethod]
    public void Launcher_UsesTheDocumentedWindowsFocusSettingsUri()
    {
        var sourcePath = Path.Combine(
            AppContext.BaseDirectory,
            "Applications",
            "WindowsFocusSettingsLauncher.cs");
        var source = File.ReadAllText(sourcePath);

        StringAssert.Contains(source, "ms-settings:quiethours");
        StringAssert.Contains(source, "Launcher.LaunchUriAsync");
    }
}
