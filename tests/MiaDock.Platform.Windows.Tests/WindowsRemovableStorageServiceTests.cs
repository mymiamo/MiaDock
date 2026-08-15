using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Platform.Windows.Input;

namespace MiaDock.Platform.Windows.Tests;

[TestClass]
public sealed class WindowsRemovableStorageServiceTests
{
    [TestMethod]
    public async Task EjectWithoutDeviceInstance_ReturnsUnsupportedWithoutNativeCall()
    {
        var service = new WindowsRemovableStorageService();
        var storage = new RemovableStorageInfo(
            "E:", "USB", "E:\\", "FAT32", 100, 50, true, null, false);

        var result = await service.EjectAsync(storage);

        Assert.AreEqual(RemovableStorageEjectStatus.Unsupported, result.Status);
        Assert.IsFalse(result.Succeeded);
    }
}
