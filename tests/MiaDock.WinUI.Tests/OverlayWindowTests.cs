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
        Assert.IsNotNull(island.Attribute("RightTapped"));
        Assert.IsFalse(document.Descendants().Any(element =>
            element.Name.LocalName is "MenuFlyout" or "MenuFlyoutItem"));
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
        StringAssert.Contains(expanded, "MixerStatusText");
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
        var overlay = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "OverlayWindow.xaml.cs"));
        var glass = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "ColorlessGlassBackdrop.cs"));

        StringAssert.Contains(glass, "FallbackColor = Color.FromArgb(0, 0, 0, 0)");
        StringAssert.Contains(overlay, "ApplyTransparentWindowBackdrop();");
    }

    [TestMethod]
    public void OverlayWindow_KeepsTheHwndTransparentSoNoBlackRectangleSurroundsTheDock()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "OverlayWindow.xaml.cs"));

        // Window.SystemBackdrop shares the composition slot used for the manual
        // transparency brush, so an unqualified assignment on the window itself
        // leaves an opaque HWND rectangle around the dock.
        var windowAssignments = source
            .Split('\n')
            .Where(line => line.Trim().StartsWith("SystemBackdrop =", StringComparison.Ordinal))
            .ToArray();

        Assert.IsEmpty(windowAssignments);
        StringAssert.Contains(source, "_windowBackdropTarget.SystemBackdrop = _transparentWindowBackdrop;");
    }

    [TestMethod]
    public void OverlayTeardown_NeverLetsManagedExceptionsEscapeClosed()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "OverlayWindow.xaml.cs"));

        var onClosed = source.IndexOf("private void OnClosed(", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, onClosed);
        var bodyStart = source.IndexOf('{', onClosed);
        var tryIndex = source.IndexOf("try", bodyStart, StringComparison.Ordinal);
        var catchIndex = source.IndexOf("catch (Exception exception)", bodyStart, StringComparison.Ordinal);
        Assert.IsGreaterThan(bodyStart, tryIndex);
        Assert.IsGreaterThan(tryIndex, catchIndex);
        StringAssert.Contains(source, "ClearTransparentWindowBackdrop");
        StringAssert.Contains(
            source,
            "Detaching and disposing the manual transparent brush races with DWM");
    }

    [TestMethod]
    public void DockMaterials_AreElementScopedSoRoundedCornersStayAntiAliased()
    {
        var overlay = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Windows",
            "OverlayWindow.xaml.cs"));
        var shell = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "IslandShell.xaml.cs"));
        var glass = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Controls",
            "ColorlessGlassBackdrop.cs"));

        // A window level material would paint the whole HWND rectangle, which
        // only a 1-bit GDI region could round.
        Assert.DoesNotContain("AddSystemBackdropTarget", overlay, StringComparison.Ordinal);
        StringAssert.Contains(glass, "AddSystemBackdropTarget(connectedTarget)");
        StringAssert.Contains(shell, "BackdropSurface.SystemBackdrop = theme switch");
        StringAssert.Contains(shell, "new ColorlessGlassBackdrop()");
    }
}
