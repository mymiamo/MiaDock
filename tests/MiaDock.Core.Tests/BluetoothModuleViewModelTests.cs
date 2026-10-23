using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using MiaDock.Modules.DeviceStatus.ViewModels;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class BluetoothModuleViewModelTests
{
    [TestMethod]
    public void ConnectionChanges_NotifyFilledIcon_OnlyForConnectedDevices()
    {
        var service = new FakeBluetoothService();
        using var viewModel = new BluetoothModuleViewModel(service);
        var changes = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        var device = new BluetoothDeviceState("headset", "Headset", false, true);
        Assert.IsFalse(viewModel.HasConnectedDevices);
        foreach (var connected in new[] { false, true, false })
        {
            changes.Clear();
            service.Publish(BluetoothStatusSnapshot.Default with
            {
                State = DeviceServiceState.Ready, IsEnumerationComplete = true,
                RadioState = BluetoothRadioState.On, Devices = [device with { IsConnected = connected }]
            });
            Assert.AreEqual(connected, viewModel.HasConnectedDevices);
            CollectionAssert.Contains(changes, nameof(viewModel.HasConnectedDevices));
        }
    }

    private sealed class FakeBluetoothService : IBluetoothStatusService
    {
        public BluetoothStatusSnapshot Current { get; private set; } = BluetoothStatusSnapshot.Default;
        public event EventHandler<BluetoothStatusSnapshot>? SnapshotChanged;
        public ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IAsyncDisposable>(new Lease());
        public void Publish(BluetoothStatusSnapshot snapshot)
        {
            Current = snapshot;
            SnapshotChanged?.Invoke(this, snapshot);
        }
        public void Dispose() { }
        private sealed class Lease : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
