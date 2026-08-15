using MiaDock.Core.Threading;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Platform.Windows.Bluetooth;

namespace MiaDock.Platform.Windows.Tests.Bluetooth;

[TestClass]
public sealed class WindowsBluetoothStatusServiceTests
{
    [TestMethod]
    public async Task RadioOffUnknownUnavailableTransitionsNeverExposeStaleDevices()
    {
        var radio = new FakeRadioProvider(BluetoothRadioState.Off);
        var service = new WindowsBluetoothStatusService(new ImmediateDispatcher(), radio);

        await using var lease = await service.AcquireAsync();
        Assert.AreEqual(BluetoothRadioState.Off, service.Current.RadioState);

        radio.Publish(BluetoothRadioState.Unknown);
        Assert.AreEqual(BluetoothRadioState.Unknown, service.Current.RadioState);
        Assert.IsEmpty(service.Current.Devices);

        radio.Publish(BluetoothRadioState.Unavailable);
        Assert.AreEqual(DeviceServiceState.Unavailable, service.Current.State);
        Assert.IsEmpty(service.Current.Devices);

        radio.Publish(BluetoothRadioState.Off);
        Assert.AreEqual(BluetoothRadioState.Off, service.Current.RadioState);
        Assert.IsFalse(service.Current.IsEnumerationComplete);
        Assert.IsEmpty(service.Current.Devices);
        Assert.AreEqual(1, radio.StartCount);
        service.Dispose();
    }

    [TestMethod]
    public async Task DisposeRejectsLateRadioCallbacks()
    {
        var radio = new FakeRadioProvider(BluetoothRadioState.Off);
        var service = new WindowsBluetoothStatusService(new ImmediateDispatcher(), radio);
        await using var lease = await service.AcquireAsync();
        var before = service.Current;

        service.Dispose();
        radio.Publish(BluetoothRadioState.Unavailable);

        Assert.AreSame(before, service.Current);
        Assert.AreEqual(1, radio.StopCount);
    }

    [TestMethod]
    public async Task OverlappingLeases_KeepSharedWatcherAliveUntilFinalRelease()
    {
        var radio = new FakeRadioProvider(BluetoothRadioState.Off);
        var service = new WindowsBluetoothStatusService(new ImmediateDispatcher(), radio);
        var first = await service.AcquireAsync();
        var second = await service.AcquireAsync();

        Assert.AreEqual(1, radio.StartCount);
        await first.DisposeAsync();
        Assert.AreEqual(0, radio.StopCount);

        await second.DisposeAsync();
        Assert.AreEqual(1, radio.StopCount);
        service.Dispose();
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public bool HasThreadAccess => true;
        public bool TryEnqueue(Action callback) { callback(); return true; }
    }

    private sealed class FakeRadioProvider(BluetoothRadioState initial) : IBluetoothRadioStateProvider
    {
        public BluetoothRadioState Current { get; private set; } = initial;
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public event EventHandler<BluetoothRadioState>? StateChanged;
        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            StartCount++;
            return Task.CompletedTask;
        }
        public void Stop() { StopCount++; }
        public void Publish(BluetoothRadioState state)
        {
            Current = state;
            StateChanged?.Invoke(this, state);
        }
        public void Dispose() { }
    }
}
