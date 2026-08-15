using System.Xml.Linq;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class ClipboardPeekExperienceTests
{
    [TestMethod]
    public void ExpandedView_HasSingleScrollOwnerSelectableHistoryAndLiveStatus()
    {
        var document = LoadControl("ClipboardPeekExpandedView.xaml");
        var text = document.ToString();

        Assert.AreEqual(1, document.Descendants().Count(element => element.Name.LocalName == "ScrollViewer"));
        Assert.AreEqual(0, document.Descendants().Count(element => element.Name.LocalName == "ListView"));
        Assert.IsGreaterThanOrEqualTo(1, document.Descendants().Count(element => element.Name.LocalName == "ItemsControl"));
        StringAssert.Contains(text, "SelectItemCommand");
        StringAssert.Contains(text, "InfoBar");
        StringAssert.Contains(text, "AutomationProperties.LiveSetting");
        StringAssert.Contains(text, "MinHeight=\"44\"");
        StringAssert.Contains(text, "CommandBar");
        StringAssert.Contains(text, "CopyColorFormatCommand");
        StringAssert.Contains(text, "ColorHexText");
        StringAssert.Contains(text, "ColorRgbText");
        StringAssert.Contains(text, "ColorHslText");
        StringAssert.Contains(text, "TextStatsText");
    }

    [TestMethod]
    public void ClipboardPeek_UsesDedicatedCompactAndNotificationViews()
    {
        Assert.IsTrue(File.Exists(Path.Combine(AppContext.BaseDirectory, "Controls", "ClipboardPeekCompactView.xaml")));
        Assert.IsTrue(File.Exists(Path.Combine(AppContext.BaseDirectory, "Controls", "ClipboardPeekNotificationView.xaml")));
        var compact = LoadControl("ClipboardPeekCompactView.xaml").ToString();
        StringAssert.Contains(compact, "CompactDetailText");
        Assert.DoesNotContain("CopyColorFormatCommand", compact, StringComparison.Ordinal);
        var notification = LoadControl("ClipboardPeekNotificationView.xaml").ToString();
        StringAssert.Contains(notification, "Commands");
        StringAssert.Contains(notification, "Peek.Thumbnail");
        StringAssert.Contains(notification, "Peek.ColorPreviewBrush");
        StringAssert.Contains(notification, "MinHeight=\"44\"");
    }

    [TestMethod]
    public void ClipboardPeekSettings_AreLocalizedAndRemoveLegacyPrivacyToggles()
    {
        var settings = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory, "Settings", "ModulesSettingsPage.xaml")).ToString();

        StringAssert.Contains(settings, "ClipboardHistoryLimits");
        StringAssert.Contains(settings, "ClipboardEventModes");
        StringAssert.Contains(settings, "ClipboardImageEvents");
        StringAssert.Contains(settings, "ClearClipboardHistoryCommand");
        Assert.DoesNotContain("ClipboardHideSensitiveContent", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("ClipboardClearHistoryOnExit", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("Deneysel", settings, StringComparison.Ordinal);
    }

    [TestMethod]
    public void ClipboardPeekResources_HaveSameKeysInAllSixLanguages()
    {
        var cultures = new[] { "tr-TR", "en-US", "az-Latn-AZ", "es-ES", "es-MX", "pt-BR" };
        HashSet<string>? expected = null;
        foreach (var culture in cultures)
        {
            var document = XDocument.Load(Path.Combine(
                AppContext.BaseDirectory, "Strings", culture, "Resources.resw"));
            var keys = document.Root!.Elements("data")
                .Select(element => element.Attribute("name")?.Value)
                .Where(name => name?.StartsWith("ClipboardPeek.", StringComparison.Ordinal) == true)
                .Cast<string>()
                .ToHashSet(StringComparer.Ordinal);
            expected ??= keys;
            Assert.IsTrue(expected.SetEquals(keys), $"Clipboard Peek resource mismatch in {culture}.");
            Assert.HasCount(45, keys);
        }
    }

    private static XDocument LoadControl(string name) =>
        XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Controls", name));
}
