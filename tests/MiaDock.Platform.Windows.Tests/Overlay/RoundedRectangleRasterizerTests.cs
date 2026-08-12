using MiaDock.Platform.Windows.Overlay;
using MiaDock.Core.Presentation;

namespace MiaDock.Platform.Windows.Tests.Overlay;

[TestClass]
public sealed class RoundedRectangleRasterizerTests
{
    [TestMethod]
    public void RenderPremultipliedBgra_CreatesTransparentCornersAndOpaqueCenter()
    {
        var pixels = RoundedRectangleRasterizer.RenderPremultipliedBgra(
            100,
            40,
            20,
            0xFF202020);

        Assert.AreEqual(0, Alpha(pixels[0]));
        Assert.AreEqual(byte.MaxValue, Alpha(pixels[20 * 100 + 50]));
    }

    [TestMethod]
    public void RenderPremultipliedBgra_ContainsFractionalAlphaAtRoundedEdge()
    {
        var pixels = RoundedRectangleRasterizer.RenderPremultipliedBgra(
            100,
            40,
            20,
            0xFF000000);

        Assert.IsTrue(pixels.Any(pixel => Alpha(pixel) is > 0 and < byte.MaxValue));
    }

    [TestMethod]
    public void RenderPremultipliedBgra_EdgeOnlySurfaceLeavesCenterTransparent()
    {
        var pixels = RoundedRectangleRasterizer.RenderPremultipliedBgra(
            100,
            40,
            20,
            0xFF000000,
            2);

        Assert.AreEqual(0, Alpha(pixels[20 * 100 + 50]));
        Assert.IsTrue(pixels.Any(pixel => Alpha(pixel) is > 0 and < byte.MaxValue));
        Assert.IsTrue(pixels.Any(pixel => Alpha(pixel) == byte.MaxValue));
    }

    [TestMethod]
    public void RenderPremultipliedBgra_PremultipliesColorChannels()
    {
        var pixels = RoundedRectangleRasterizer.RenderPremultipliedBgra(
            40,
            40,
            20,
            0x80FF8040);
        var center = pixels[20 * 40 + 20];

        Assert.AreEqual(0x80, Alpha(center));
        Assert.AreEqual(0x80, (center >> 16) & 0xFF);
        Assert.AreEqual(0x40, (center >> 8) & 0xFF);
        Assert.AreEqual(0x20, center & 0xFF);
    }

    [TestMethod]
    public void RenderPremultipliedBgra_UsesIndependentCornerRadii()
    {
        var pixels = RoundedRectangleRasterizer.RenderPremultipliedBgra(
            100,
            40,
            new DockCornerRadii(0, 20, 0, 20),
            0xFF202020);

        Assert.AreEqual(byte.MaxValue, Alpha(pixels[0]));
        Assert.AreEqual(0, Alpha(pixels[99]));
        Assert.AreEqual(byte.MaxValue, Alpha(pixels[39 * 100 + 99]));
        Assert.AreEqual(0, Alpha(pixels[39 * 100]));
    }

    private static int Alpha(int pixel) => (pixel >> 24) & 0xFF;
}
