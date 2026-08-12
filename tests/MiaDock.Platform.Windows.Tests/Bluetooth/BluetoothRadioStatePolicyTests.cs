using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Platform.Windows.Bluetooth;
using Windows.Devices.Radios;

namespace MiaDock.Platform.Windows.Tests.Bluetooth;

[TestClass]
public sealed class BluetoothRadioStatePolicyTests
{
    [TestMethod]
    [DataRow(BluetoothRadioState.Off, DeviceServiceState.Ready)]
    [DataRow(BluetoothRadioState.Unknown, DeviceServiceState.Starting)]
    [DataRow(BluetoothRadioState.Unavailable, DeviceServiceState.Unavailable)]
    public void CreateNonDiscoveringSnapshot_InvalidatesEveryCachedDevice(
        BluetoothRadioState state,
        DeviceServiceState expectedServiceState)
    {
        var result = BluetoothRadioStatePolicy.CreateNonDiscoveringSnapshot(state);

        Assert.AreEqual(state, result.RadioState);
        Assert.AreEqual(expectedServiceState, result.State);
        Assert.IsFalse(result.IsEnumerationComplete);
        Assert.IsEmpty(result.Devices);
    }

    [TestMethod]
    public void Map_DistinguishesOnOffAndUnknownRadioStates()
    {
        Assert.AreEqual(BluetoothRadioState.On, WindowsBluetoothRadioStateProvider.Map(RadioState.On));
        Assert.AreEqual(BluetoothRadioState.Off, WindowsBluetoothRadioStateProvider.Map(RadioState.Off));
        Assert.AreEqual(BluetoothRadioState.Off, WindowsBluetoothRadioStateProvider.Map(RadioState.Disabled));
        Assert.AreEqual(BluetoothRadioState.Unknown, WindowsBluetoothRadioStateProvider.Map(RadioState.Unknown));
    }
}
