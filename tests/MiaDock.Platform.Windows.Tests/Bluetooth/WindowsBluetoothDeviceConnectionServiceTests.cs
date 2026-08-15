using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using MiaDock.Platform.Windows.Bluetooth;

namespace MiaDock.Platform.Windows.Tests.Bluetooth;

[TestClass]
public sealed class WindowsBluetoothDeviceConnectionServiceTests
{
    [TestMethod]
    public async Task RadioOff_ReturnsRadioOff()
    {
        var service = Create(BluetoothRadioState.Off);

        var result = await service.ConnectAsync(new BluetoothConnectionRequest("endpoint", "AA:BB:CC:DD:EE:FF", DeviceHubDeviceType.Headset));

        Assert.AreEqual(BluetoothConnectionResult.RadioOff, result);
    }

    [TestMethod]
    public async Task EmptyIds_ReturnUnavailable()
    {
        var service = Create(BluetoothRadioState.On);

        var result = await service.ConnectAsync(new BluetoothConnectionRequest(null, null, DeviceHubDeviceType.Headset));

        Assert.AreEqual(BluetoothConnectionResult.Unavailable, result);
    }

    [TestMethod]
    public async Task Connect_EnablesProfilesThenAcl()
    {
        var profiles = new FakeProfiles();
        var acl = new FakeAcl();
        var service = Create(BluetoothRadioState.On, profiles, acl);

        var result = await service.ConnectAsync(new BluetoothConnectionRequest(
            "endpoint", "AA:BB:CC:DD:EE:FF", DeviceHubDeviceType.Headphones));

        Assert.AreEqual(BluetoothConnectionResult.Succeeded, result);
        Assert.IsTrue(profiles.LastEnable);
        Assert.AreEqual(0xAABBCCDDEEFFUL, profiles.LastAddress);
        Assert.AreEqual("endpoint", acl.LastEndpointId);
        Assert.AreEqual(2, profiles.LastServices?.Count);
    }

    [TestMethod]
    public async Task UnknownRadio_ReturnsUnavailable()
    {
        var service = Create(BluetoothRadioState.Unknown);

        var result = await service.ConnectAsync(new BluetoothConnectionRequest("endpoint", "AA:BB:CC:DD:EE:FF", DeviceHubDeviceType.Headset));

        Assert.AreEqual(BluetoothConnectionResult.Unavailable, result);
    }

    [TestMethod]
    public async Task Connect_WithoutAddress_UsesAclEndpoint()
    {
        var profiles = new FakeProfiles();
        var acl = new FakeAcl();
        var service = Create(BluetoothRadioState.On, profiles, acl);

        var result = await service.ConnectAsync(new BluetoothConnectionRequest(
            "endpoint", null, DeviceHubDeviceType.Headphones));

        Assert.AreEqual(BluetoothConnectionResult.Succeeded, result);
        Assert.IsNull(profiles.LastServices);
        Assert.AreEqual("endpoint", acl.LastEndpointId);
    }

    [TestMethod]
    public async Task Disconnect_WithoutAddress_ReturnsUnavailable()
    {
        var profiles = new FakeProfiles();
        var acl = new FakeAcl();
        var service = Create(BluetoothRadioState.On, profiles, acl);

        var result = await service.DisconnectAsync(new BluetoothConnectionRequest(
            "endpoint", null, DeviceHubDeviceType.Headphones));

        Assert.AreEqual(BluetoothConnectionResult.Unavailable, result);
        Assert.IsNull(acl.LastEndpointId);
        Assert.IsNull(profiles.LastServices);
    }

    [TestMethod]
    public async Task Disconnect_DisablesProfilesWithoutAcl()
    {
        var profiles = new FakeProfiles();
        var acl = new FakeAcl();
        var service = Create(BluetoothRadioState.On, profiles, acl);

        var result = await service.DisconnectAsync(new BluetoothConnectionRequest(
            "endpoint", "AA:BB:CC:DD:EE:FF", DeviceHubDeviceType.Mouse));

        Assert.AreEqual(BluetoothConnectionResult.Succeeded, result);
        Assert.IsFalse(profiles.LastEnable);
        Assert.IsNull(acl.LastEndpointId);
        Assert.AreEqual(BluetoothNative.HumanInterfaceDevice, profiles.LastServices?.Single());
    }

    private static WindowsBluetoothDeviceConnectionService Create(
        BluetoothRadioState radio,
        FakeProfiles? profiles = null,
        FakeAcl? acl = null) =>
        new(new FakeRadio(radio), profiles ?? new FakeProfiles(), acl ?? new FakeAcl());

    private sealed class FakeRadio(BluetoothRadioState current) : IBluetoothRadioStateProvider
    {
        public BluetoothRadioState Current { get; } = current;
        public event EventHandler<BluetoothRadioState>? StateChanged { add { } remove { } }
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Stop() { }
        public void Dispose() { }
    }

    private sealed class FakeProfiles : IBluetoothProfileController
    {
        public ulong LastAddress { get; private set; }
        public bool LastEnable { get; private set; }
        public IReadOnlyList<Guid>? LastServices { get; private set; }
        public BluetoothProfileOperationResult Result { get; set; } = BluetoothProfileOperationResult.Succeeded;

        public BluetoothProfileOperationResult SetServices(ulong address, IReadOnlyList<Guid> services, bool enable)
        {
            LastAddress = address;
            LastEnable = enable;
            LastServices = services;
            return Result;
        }
    }

    private sealed class FakeAcl : IBluetoothAclConnector
    {
        public string? LastEndpointId { get; private set; }
        public BluetoothConnectionResult Result { get; set; } = BluetoothConnectionResult.Succeeded;

        public Task<BluetoothConnectionResult> ConnectAsync(string endpointId, CancellationToken cancellationToken)
        {
            LastEndpointId = endpointId;
            return Task.FromResult(Result);
        }
    }
}
