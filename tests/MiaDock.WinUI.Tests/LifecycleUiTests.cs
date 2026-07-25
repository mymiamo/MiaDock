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
}
