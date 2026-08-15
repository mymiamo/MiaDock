using MiaDock.Core.Overlay;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class DockEdgeRevealGeometryTests
{
    private static readonly OverlayWorkArea Bounds = new(1920, 0, 2560, 1440);
    private static readonly OverlayPlacement Placement = new(3000, 12, 300, 48);

    [TestMethod]
    [DataRow(OverlayPosition.TopCenter, 3000, -46)]
    [DataRow(OverlayPosition.BottomCenter, 3000, 1438)]
    [DataRow(OverlayPosition.LeftCenter, 1622, 12)]
    [DataRow(OverlayPosition.RightCenter, 4478, 12)]
    public void HideTowardAttachedEdge_LeavesTwoPixelStrip(
        OverlayPosition position,
        int expectedX,
        int expectedY)
    {
        var result = DockEdgeRevealGeometry.HideTowardAttachedEdge(
            Placement,
            Bounds,
            position,
            2);

        Assert.AreEqual(expectedX, result.X);
        Assert.AreEqual(expectedY, result.Y);
    }

    [TestMethod]
    [DataRow(OverlayPosition.TopCenter, -36)]
    [DataRow(OverlayPosition.BottomCenter, 1428)]
    public void HideTowardAttachedEdge_LeavesTwelvePixelHandle(
        OverlayPosition position,
        int expectedY)
    {
        var result = DockEdgeRevealGeometry.HideTowardAttachedEdge(
            Placement,
            Bounds,
            position,
            12);

        Assert.AreEqual(expectedY, result.Y);
        Assert.AreEqual(Placement.Width, result.Width);
    }

    [TestMethod]
    public void IsPointerAtActivationEdge_RequiresCorrectDisplayAndDockSpan()
    {
        Assert.IsTrue(DockEdgeRevealGeometry.IsPointerAtActivationEdge(
            3100, 1, Bounds, Placement, OverlayPosition.TopCenter, 3, 24));
        Assert.IsFalse(DockEdgeRevealGeometry.IsPointerAtActivationEdge(
            100, 1, Bounds, Placement, OverlayPosition.TopCenter, 3, 24));
        Assert.IsFalse(DockEdgeRevealGeometry.IsPointerAtActivationEdge(
            3100, 20, Bounds, Placement, OverlayPosition.TopCenter, 3, 24));
    }
}
