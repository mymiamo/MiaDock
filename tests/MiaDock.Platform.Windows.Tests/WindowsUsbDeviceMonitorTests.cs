using MiaDock.Platform.Windows.Input;

namespace MiaDock.Platform.Windows.Tests;

[TestClass]
public sealed class WindowsUsbDeviceMonitorTests
{
    [TestMethod]
    public void EnumerateDriveLetters_ExpandsUnitMaskBits()
    {
        var letters = WindowsUsbDeviceMonitor.EnumerateDriveLetters(0b1010).ToArray();

        CollectionAssert.AreEqual(new[] { 'B', 'D' }, letters);
    }

    [TestMethod]
    public void EnumerateDriveLetters_EmptyMask_ReturnsNoLetters()
    {
        Assert.AreEqual(0, WindowsUsbDeviceMonitor.EnumerateDriveLetters(0).Count());
    }
}
