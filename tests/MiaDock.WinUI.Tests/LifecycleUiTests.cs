using System.Xml.Linq;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class LifecycleUiTests
{
    [TestMethod]
    public void CloseDialog_OffersMinimizeExitAndRememberOptions()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Dialogs",
            "CloseBehaviorDialog.xaml"));
        var attributes = document.Descendants().Attributes().Select(attribute => attribute.Value).ToArray();

        Assert.IsTrue(attributes.Contains("Sistem tepsisine küçült"));
        Assert.IsTrue(attributes.Contains("Uygulamadan tamamen çık"));
        Assert.IsTrue(attributes.Contains("Seçimimi hatırla"));
    }

    [TestMethod]
    public void StartupPage_ExplainsStoreCompatibleStartupTask()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "StartupShutdownSettingsPage.xaml"));
        var messages = document.Descendants().Attributes("Message").Select(attribute => attribute.Value);

        Assert.IsTrue(messages.Any(message => message.Contains("StartupTask API", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void PackageManifest_DeclaresStartupTaskUsedByApplication()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Package.appxmanifest"));
        XNamespace desktop = "http://schemas.microsoft.com/appx/manifest/desktop/windows10";

        var extension = document
            .Descendants(desktop + "Extension")
            .Single(element => (string?)element.Attribute("Category") == "windows.startupTask");
        var startupTask = extension.Element(desktop + "StartupTask");

        Assert.IsNotNull(startupTask);
        Assert.AreEqual("MiaDockStartupTask", (string?)startupTask.Attribute("TaskId"));
        Assert.AreEqual("false", (string?)startupTask.Attribute("Enabled"));
        Assert.AreEqual("MiaDock", (string?)startupTask.Attribute("DisplayName"));
        Assert.AreEqual("MiaDock.App.exe", (string?)extension.Attribute("Executable"));
        Assert.AreEqual("Windows.FullTrustApplication", (string?)extension.Attribute("EntryPoint"));
    }

    [TestMethod]
    public void ApplicationProject_PackagesTimerAlarmWaveFile()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "MiaDock.App.csproj"));
        var content = document
            .Descendants("Content")
            .Single(element =>
                (string?)element.Attribute("Include") == @"Assets\miadock-ringtone.wav");

        Assert.AreEqual(
            "PreserveNewest",
            (string?)content.Attribute("CopyToOutputDirectory"));
    }
}
