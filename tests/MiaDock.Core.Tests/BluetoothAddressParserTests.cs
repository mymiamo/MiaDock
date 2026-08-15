using MiaDock.Modules.DeviceStatus.Services;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class BluetoothAddressParserTests
{
    [TestMethod]
    public void TryParse_ColonSeparatedMac()
    {
        Assert.IsTrue(BluetoothAddressParser.TryParse("AA:BB:CC:DD:EE:FF", out var address));
        Assert.AreEqual(0xAABBCCDDEEFFUL, address);
    }

    [TestMethod]
    public void TryExtractFromEndpointId_UsesTrailingAddress()
    {
        var extracted = BluetoothAddressParser.TryExtractFromEndpointId(
            "Bluetooth#Bluetooth00:11:22:33:44:55-aa:bb:cc:dd:ee:ff");

        Assert.AreEqual("aa:bb:cc:dd:ee:ff", extracted);
        Assert.IsTrue(BluetoothAddressParser.TryParse(extracted, out var address));
        Assert.AreEqual(0xAABBCCDDEEFFUL, address);
    }

    [TestMethod]
    public void TryParse_RejectsEmpty()
    {
        Assert.IsFalse(BluetoothAddressParser.TryParse(" ", out _));
        Assert.IsFalse(BluetoothAddressParser.TryParse(null, out _));
    }
}
