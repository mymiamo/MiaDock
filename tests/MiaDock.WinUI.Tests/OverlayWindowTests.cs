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
}
