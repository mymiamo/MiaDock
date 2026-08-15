using System.Xml.Linq;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class AudibleNotificationsExperienceTests
{
    [TestMethod]
    public void SettingsPage_HasMasterSixEventTogglesAndAccessiblePreviews()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Settings",
            "AudibleNotificationsSettingsPage.xaml"));
        var text = document.ToString();

        Assert.AreEqual(1, document.Descendants().Count(element => element.Name.LocalName == "ScrollViewer"));
        Assert.AreEqual(7, document.Descendants().Count(element => element.Name.LocalName == "ToggleSwitch"));
        Assert.AreEqual(6, document.Descendants().Count(element => element.Name.LocalName == "Button"));
        Assert.AreEqual(1, document.Descendants().Count(element => element.Name.LocalName == "InfoBar"));
        StringAssert.Contains(text, "AudibleNotificationControlsEnabled");
        StringAssert.Contains(text, "PreviewNetworkOfflineSoundCommand");
        StringAssert.Contains(text, "PreviewConnectedWithoutInternetSoundCommand");
        StringAssert.Contains(text, "PreviewLowBatterySoundCommand");
        StringAssert.Contains(text, "PreviewDeviceConnectedSoundCommand");
        StringAssert.Contains(text, "PreviewDeviceDisconnectedSoundCommand");
        StringAssert.Contains(text, "PreviewHourlySoundCommand");
        StringAssert.Contains(text, "AutomationProperties.Name");
        StringAssert.Contains(text, "MinHeight=\"44\"");
    }

    [TestMethod]
    public void SettingsNavigation_ListsNotificationSoundsUnderPersonalization()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "SettingsWindow.xaml.cs"));

        StringAssert.Contains(source, "[\"audible-notifications\"]");
        StringAssert.Contains(source, "Subpage(\"audible-notifications\", \"personalization\"");
        StringAssert.Contains(source, "Search(\"Sesli Bildirimler\", \"Notification Sounds\"");
    }

    [TestMethod]
    public void AudibleNotificationResources_HaveSameKeysInAllSixLanguages()
    {
        var cultures = new[] { "tr-TR", "en-US", "az-Latn-AZ", "es-ES", "es-MX", "pt-BR" };
        HashSet<string>? expected = null;
        foreach (var culture in cultures)
        {
            var document = XDocument.Load(Path.Combine(
                AppContext.BaseDirectory,
                "Strings",
                culture,
                "Resources.resw"));
            var keys = document.Root!.Elements("data")
                .Select(element => element.Attribute("name")?.Value)
                .Where(name => name?.StartsWith("AudibleNotifications.", StringComparison.Ordinal) == true)
                .Cast<string>()
                .ToHashSet(StringComparer.Ordinal);
            expected ??= keys;
            Assert.IsTrue(expected.SetEquals(keys), $"Notification sound resource mismatch in {culture}.");
            Assert.HasCount(20, keys);
        }
    }

    [TestMethod]
    public void PackageCopiesTheSixNotificationSoundsIncludingHourly()
    {
        var project = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "MiaDock.App.csproj"));

        foreach (var file in new[]
                 {
                     "connected-internet-none.wav",
                     "connected-but-no-internet.wav",
                     "low-battery.wav",
                     "device-connected.wav",
                     "device-left.wav",
                     "per-hour-per.wav"
                 })
        {
            StringAssert.Contains(project, file);
        }

        StringAssert.Contains(project, "<Content Include=\"Assets\\sfx\\per-hour-per.wav\"");
    }

    [TestMethod]
    public void HourlyNotificationResources_HaveSameKeysInAllSixLanguages()
    {
        var cultures = new[] { "tr-TR", "en-US", "az-Latn-AZ", "es-ES", "es-MX", "pt-BR" };
        HashSet<string>? expected = null;
        foreach (var culture in cultures)
        {
            var document = XDocument.Load(Path.Combine(
                AppContext.BaseDirectory,
                "Strings",
                culture,
                "Resources.resw"));
            var keys = document.Root!.Elements("data")
                .Select(element => element.Attribute("name")?.Value)
                .Where(name => name?.StartsWith("HourlyNotification.", StringComparison.Ordinal) == true)
                .Cast<string>()
                .ToHashSet(StringComparer.Ordinal);
            expected ??= keys;
            Assert.IsTrue(expected.SetEquals(keys), $"Hourly notification resource mismatch in {culture}.");
            Assert.HasCount(6, keys);
        }
    }

    [TestMethod]
    public void Overlay_PlaysOnlyTheActiveEventAfterPresentationPolicyAllowsIt()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "OverlayWindow.xaml.cs"));

        StringAssert.Contains(source, "nameof(_viewModel.Island.LastModuleEvent)");
        StringAssert.Contains(source, "TryPlayActiveAudibleEvent");
        StringAssert.Contains(source, "CanPresentModuleEvent(moduleEvent)");
        StringAssert.Contains(source, "AudibleNotifications.Allows(moduleEvent.AudibleCue)");
        StringAssert.Contains(source, "_audibleNotificationPlayer.Play(moduleEvent.AudibleCue)");
    }
}
