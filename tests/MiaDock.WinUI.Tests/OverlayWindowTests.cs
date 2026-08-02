using System.Xml.Linq;

namespace MiaDock.WinUI.Tests;

[TestClass]
public sealed class OverlayWindowTests
{
    [TestMethod]
    public void OverlayWindow_UsesTransparentClippedRootAndIslandPointerEvents()
    {
        var document = XDocument.Load(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "OverlayWindow.xaml"));
        var root = document.Descendants().Single(element =>
            element.Attribute(XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value == "Root");
        var island = document.Descendants().Single(element => element.Name.LocalName == "IslandShell");

        Assert.AreEqual("Transparent", root.Attribute("Background")?.Value);
        Assert.IsNotNull(island.Attribute("PointerEntered"));
        Assert.IsNotNull(island.Attribute("PointerExited"));
        Assert.IsNotNull(island.Attribute("PointerMoved"));
        Assert.IsNotNull(island.Attribute("PointerWheelChanged"));
        Assert.IsNotNull(island.Attribute("Tapped"));
        Assert.IsNotNull(island.Attribute("KeyDown"));
        Assert.AreEqual("OnDefaultModuleRequested", island.Attribute("DefaultModuleRequested")?.Value);
        Assert.IsTrue(document.Descendants().Any(element =>
            element.Name.LocalName == "MenuFlyoutItem" &&
            element.Attribute("Text")?.Value == "Ayarlar"));
    }

    [TestMethod]
    public void VolumeViews_ProvideCompactExpandedAndFullscreenControls()
    {
        var controlsDirectory = Path.Combine(AppContext.BaseDirectory, "Controls");
        var compact = XDocument.Load(Path.Combine(
            controlsDirectory,
            "VolumeCompactView.xaml")).ToString();
        var expanded = XDocument.Load(Path.Combine(
            controlsDirectory,
            "VolumeExpandedView.xaml")).ToString();
        var notification = XDocument.Load(Path.Combine(
            controlsDirectory,
            "VolumeNotificationView.xaml")).ToString();

        StringAssert.Contains(compact, "VolumeGlyph");
        StringAssert.Contains(compact, "VolumeText");
        StringAssert.Contains(expanded, "Snapshot.MasterVolumePercent");
        StringAssert.Contains(expanded, "ToggleMuteCommand");
        StringAssert.Contains(expanded, "OpenSoundSettingsFromUiCommand");
        StringAssert.Contains(expanded, "MixerSessions");
        StringAssert.Contains(expanded, "Snapshot.PeakLevel");
        StringAssert.Contains(expanded, "OnSessionVolumeChanged");
        StringAssert.Contains(expanded, "Snapshot.CanControlVolume");
        StringAssert.Contains(expanded, "VolumeAutomationName");
        StringAssert.Contains(expanded, "MuteAutomationName");
        StringAssert.Contains(expanded, "AutomationProperties.AccessibilityView=\"Raw\"");
        StringAssert.Contains(expanded, "AutomationProperties.LiveSetting=\"Polite\"");
        StringAssert.Contains(notification, "VolumeProgress");
        StringAssert.Contains(notification, "VolumeSlider");
        StringAssert.Contains(notification, "SettingsButton");
        StringAssert.Contains(notification, "AutomationProperties.LiveSetting=\"Polite\"");
    }

    [TestMethod]
    public void VolumeMixer_CoalescesRapidSliderChangesAndFlushesOnUnload()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "VolumeExpandedView.xaml.cs"));

        StringAssert.Contains(source, "TimeSpan.FromMilliseconds(80)");
        StringAssert.Contains(source, "_pendingSessionVolumes[session.SessionKey]");
        StringAssert.Contains(source, "FlushPendingVolumesAsync(drainAll: true)");
        StringAssert.Contains(source, "SetPresentationActive(false)");
    }

    [TestMethod]
    public void ColorlessGlassFallback_RemainsTransparentInsteadOfPaintingBlackRectangle()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "OverlayWindow.xaml.cs"));

        StringAssert.Contains(source, "theme.UsesColorlessGlass()");
        StringAssert.Contains(source, "FallbackColor = Color.FromArgb(0, 0, 0, 0)");
        StringAssert.Contains(source, "ApplyTransparentWindowBackdrop();");
    }
}
