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
            new BluetoothDeviceState("container", "Kulaklık", false, true),
            new BluetoothDeviceState("container", "Kulaklık LE", true, true)
        });

        Assert.HasCount(1, result);
        Assert.IsTrue(result[0].IsConnected);
        Assert.IsTrue(result[0].IsPresent);
    }
}
