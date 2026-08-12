using MiaDock.Platform.Windows.Overlay;
using MiaDock.Core.Presentation;

namespace MiaDock.Platform.Windows.Tests.Overlay;

[TestClass]
public sealed class RoundedRectangleHitTestTests
{
    [TestMethod]
    public void Contains_RejectsTransparentCornerAndAcceptsRoundedSurface()
    {
        Assert.IsFalse(RoundedRectangleHitTest.Contains(0, 0, 292, 46, 23));
        Assert.IsTrue(RoundedRectangleHitTest.Contains(22, 8, 292, 46, 23));
        Assert.IsTrue(RoundedRectangleHitTest.Contains(146, 0, 292, 46, 23));
        Assert.IsTrue(RoundedRectangleHitTest.Contains(291, 23, 292, 46, 23));
    }

    [TestMethod]
    public void Contains_ClampsOversizedRadiusToCapsule()
    {
        Assert.IsFalse(RoundedRectangleHitTest.Contains(0, 0, 100, 40, 500));
        Assert.IsTrue(RoundedRectangleHitTest.Contains(20, 0, 100, 40, 500));
        Assert.IsTrue(RoundedRectangleHitTest.Contains(50, 20, 100, 40, 500));
    }

    [TestMethod]
    public void PointFromMessage_PreservesSignedScreenCoordinates()
    {
        const short x = -120;
        const short y = 840;
        var packed = new nint(
            unchecked((ushort)x) |
            (unchecked((ushort)y) << 16));

        var point = RoundedRectangleHitTest.PointFromMessage(packed);

        Assert.AreEqual(x, point.X);
        Assert.AreEqual(y, point.Y);
    }

    [TestMethod]
    public void Contains_UsesIndependentCornerRadii()
    {
        var radii = new DockCornerRadii(0, 20, 0, 20);

        Assert.IsTrue(RoundedRectangleHitTest.Contains(0, 0, 100, 40, radii));
        Assert.IsFalse(RoundedRectangleHitTest.Contains(99, 0, 100, 40, radii));
        Assert.IsTrue(RoundedRectangleHitTest.Contains(99, 39, 100, 40, radii));
        Assert.IsFalse(RoundedRectangleHitTest.Contains(0, 39, 100, 40, radii));
    }
}
