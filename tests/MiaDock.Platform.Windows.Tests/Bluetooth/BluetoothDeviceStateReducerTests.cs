using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Platform.Windows.Bluetooth;

namespace MiaDock.Platform.Windows.Tests.Bluetooth;

[TestClass]
public sealed class BluetoothDeviceStateReducerTests
{
    [TestMethod]
    public void Merge_CombinesClassicAndLowEnergyEndpoints()
    {
        var result = BluetoothDeviceStateReducer.Merge(new[]
        {
            new BluetoothDeviceState(
                "container", "Kulaklık", false, true, 40, DeviceHubDeviceType.Headphones,
                "classic-id", "AA:BB:CC:DD:EE:FF"),
            new BluetoothDeviceState(
                "container", "Kulaklık LE", true, true, 72, DeviceHubDeviceType.GenericBluetoothDevice,
                "le-id", null)
        });

        Assert.HasCount(1, result);
        Assert.IsTrue(result[0].IsConnected);
        Assert.IsTrue(result[0].IsPresent);
        Assert.AreEqual(72, result[0].BatteryPercentage);
        Assert.AreEqual(DeviceHubDeviceType.Headphones, result[0].DeviceType);
        Assert.AreEqual("le-id", result[0].EndpointId);
        Assert.AreEqual("AA:BB:CC:DD:EE:FF", result[0].DeviceAddress);
    }
}
