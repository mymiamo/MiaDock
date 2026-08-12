using MiaDock.Platform.Windows.Windowing;

namespace MiaDock.Platform.Windows.Tests.Windowing;

[TestClass]
public sealed class WindowMinimumSizeMonitorTests
{
    [TestMethod]
    public void ScaleDipToPixels_UsesCurrentWindowDpiAndRoundsUp()
    {
        Assert.AreEqual(640, WindowMinimumSizeMonitor.ScaleDipToPixels(640, 96));
        Assert.AreEqual(960, WindowMinimumSizeMonitor.ScaleDipToPixels(640, 144));
        Assert.AreEqual(641, WindowMinimumSizeMonitor.ScaleDipToPixels(640.1, 96));
    }

    [TestMethod]
    public void ScaleDipToPixels_FallsBackToStandardDpiWhenUnavailable()
    {
        Assert.AreEqual(560, WindowMinimumSizeMonitor.ScaleDipToPixels(560, 0));
    }
}
