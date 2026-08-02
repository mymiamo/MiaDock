using MiaDock.Platform.Windows.Interop;

namespace MiaDock.Platform.Windows.Tests;

[TestClass]
public sealed class WindowStylePolicyTests
{
    [TestMethod]
    public void ApplyOverlayStyles_AddsNoActivateAndToolWindowWithoutLayeredStyle()
    {
        var result = WindowStylePolicy.ApplyOverlayStyles(WindowStylePolicy.Layered);

        Assert.AreNotEqual(0, result & WindowStylePolicy.NoActivate);
        Assert.AreNotEqual(0, result & WindowStylePolicy.ToolWindow);
        Assert.AreEqual(0, result & WindowStylePolicy.Layered);
    }

    [TestMethod]
    public void ApplyOverlayStyles_RemovesAppWindow()
    {
        var result = WindowStylePolicy.ApplyOverlayStyles(WindowStylePolicy.AppWindow);

        Assert.AreEqual(0, result & WindowStylePolicy.AppWindow);
    }

    [TestMethod]
    public void ApplyOverlayStyles_PreservesUnrelatedFlags()
    {
        const long unrelatedFlag = 0x00000008;

        var result = WindowStylePolicy.ApplyOverlayStyles(unrelatedFlag);

        Assert.AreNotEqual(0, result & unrelatedFlag);
    }

    [TestMethod]
    public void ApplyOverlayStyles_RemovesNoActivateForExplicitInteraction()
    {
        var result = WindowStylePolicy.ApplyOverlayStyles(
            WindowStylePolicy.NoActivate,
            allowActivation: true);

        Assert.AreEqual(0, result & WindowStylePolicy.NoActivate);
        Assert.AreNotEqual(0, result & WindowStylePolicy.ToolWindow);
    }

    [TestMethod]
    public void ApplyOverlayWindowStyles_RemovesEveryNativeFrameDecoration()
    {
        var frameStyles = WindowStylePolicy.Caption
            | WindowStylePolicy.ThickFrame
            | WindowStylePolicy.SystemMenu
            | WindowStylePolicy.MinimizeBox
            | WindowStylePolicy.MaximizeBox;

        var result = WindowStylePolicy.ApplyOverlayWindowStyles(frameStyles);

        Assert.AreEqual(0, result & frameStyles);
    }

    [TestMethod]
    public void ApplyOverlayWindowStyles_PreservesUnrelatedFlags()
    {
        const long visibleStyle = 0x10000000;

        var result = WindowStylePolicy.ApplyOverlayWindowStyles(visibleStyle);

        Assert.AreNotEqual(0, result & visibleStyle);
    }
}
