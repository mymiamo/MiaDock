using MiaDock.Core.Overlay;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class OverlayPlacementCalculatorTests
{
    private readonly OverlayPlacementCalculator _calculator = new();

    [TestMethod]
    [DataRow(OverlayPosition.TopLeft, 12, 32)]
    [DataRow(OverlayPosition.TopCenter, 860, 32)]
    [DataRow(OverlayPosition.TopRight, 1708, 32)]
    [DataRow(OverlayPosition.BottomLeft, 12, 1008)]
    [DataRow(OverlayPosition.BottomCenter, 860, 1008)]
    [DataRow(OverlayPosition.BottomRight, 1708, 1008)]
    public void Calculate_AnchorsInsideWorkArea(OverlayPosition position, int expectedX, int expectedY)
    {
        var request = new OverlayLayoutRequest(
            new OverlayWorkArea(0, 20, 1920, 1040),
            new OverlaySize(200, 40),
            96,
            position);

        var result = _calculator.Calculate(request);

        Assert.AreEqual(new OverlayPlacement(expectedX, expectedY, 200, 40), result);
    }

    [TestMethod]
    [DataRow(96u, 200, 40, 12)]
    [DataRow(120u, 250, 50, 15)]
    [DataRow(144u, 300, 60, 18)]
    [DataRow(192u, 400, 80, 24)]
    public void Calculate_ScalesDipsToPhysicalPixels(uint dpi, int expectedWidth, int expectedHeight, int expectedMargin)
    {
        var result = _calculator.Calculate(new OverlayLayoutRequest(
            new OverlayWorkArea(-1920, 0, 1920, 1080),
            new OverlaySize(200, 40),
            dpi,
            OverlayPosition.TopLeft));

        Assert.AreEqual(expectedWidth, result.Width);
        Assert.AreEqual(expectedHeight, result.Height);
        Assert.AreEqual(-1920 + expectedMargin, result.X);
    }

    [TestMethod]
    public void Calculate_ClampsOversizedOverlayToWorkAreaOrigin()
    {
        var result = _calculator.Calculate(new OverlayLayoutRequest(
            new OverlayWorkArea(100, 200, 300, 200),
            new OverlaySize(500, 400),
            96,
            OverlayPosition.BottomRight));

        Assert.AreEqual(new OverlayPlacement(100, 200, 500, 400), result);
    }

    [TestMethod]
    public void Calculate_RejectsInvalidDpi()
    {
        var request = new OverlayLayoutRequest(
            new OverlayWorkArea(0, 0, 1920, 1080),
            new OverlaySize(200, 40),
            0,
            OverlayPosition.TopCenter);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _calculator.Calculate(request));
    }
}
