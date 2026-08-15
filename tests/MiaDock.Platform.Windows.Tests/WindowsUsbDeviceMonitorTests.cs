using MiaDock.Platform.Windows.Input;

namespace MiaDock.Platform.Windows.Tests;

[TestClass]
public sealed class WindowsUsbDeviceMonitorTests
{
    [TestMethod]
    public void StorageDevice_UsesVolumeLabelAndDriveLetter()
    {
        var result = WindowsUsbDeviceMonitor.CreateDeviceChangedEvent(
            true, "opaque-device-key", "e:", "Archive", DateTimeOffset.UtcNow);

        Assert.AreEqual("e:", result.DriveLetter);
        Assert.AreEqual("Archive (e:)", result.DisplayName);
        Assert.AreEqual("opaque-device-key", result.DeviceKey);
    }

    [TestMethod]
    public void DriverlessDevice_UsesSafeGenericName()
    {
        var result = WindowsUsbDeviceMonitor.CreateDeviceChangedEvent(
            true, "opaque-device-key", null, null, DateTimeOffset.UtcNow);

        Assert.AreEqual(string.Empty, result.DriveLetter);
        Assert.AreEqual("USB device", result.DisplayName);
    }

    [TestMethod]
    public void DuplicateDeviceBroadcast_IsCoalesced()
    {
        var coalescer = new UsbDeviceChangeCoalescer(TimeSpan.FromSeconds(2));
        var now = DateTimeOffset.UtcNow;

        Assert.IsTrue(coalescer.TryAccept("device", true, now));
        Assert.IsFalse(coalescer.TryAccept("device", true, now.AddMilliseconds(200)));
        Assert.IsTrue(coalescer.TryAccept("device", false, now.AddMilliseconds(300)));
        Assert.IsTrue(coalescer.TryAccept("device", false, now.AddSeconds(3)));
    }

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

    [TestMethod]
    public async Task OverlappingLeases_DoNotStopMonitorEarly()
    {
        await using var monitor = new WindowsUsbDeviceMonitor();
        var first = await monitor.AcquireAsync();
        var second = await monitor.AcquireAsync();

        Assert.IsTrue(monitor.IsRunning);
        await first.DisposeAsync();
        Assert.IsTrue(monitor.IsRunning);

        await second.DisposeAsync();
        Assert.IsFalse(monitor.IsRunning);
    }
}
