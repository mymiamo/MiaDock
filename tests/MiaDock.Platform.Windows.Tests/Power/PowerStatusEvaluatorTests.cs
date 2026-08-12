using MiaDock.Core.Threading;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.Time.Services;
using MiaDock.Platform.Windows.Power;

namespace MiaDock.Platform.Windows.Tests.Power;

[TestClass]
public sealed class PowerStatusEvaluatorTests
{
    [TestMethod]
    public void Evaluate_PowerSupplyNotPresentAloneDoesNotErasePhysicalBattery()
    {
        var result = PowerStatusEvaluator.Evaluate(
            new PowerStatusReading("Discharging", "NotPresent", "Battery", 64, false),
            DateTimeOffset.UtcNow);

        Assert.AreEqual(BatteryAvailabilityState.Available, result.Availability);
        Assert.IsTrue(result.IsBatteryPresent);
        Assert.AreEqual(64, result.ChargePercent);
    }

    [TestMethod]
    public void Evaluate_BatteryStatusNotPresentIsExplicitNoBatterySignal()
    {
        var result = PowerStatusEvaluator.Evaluate(
            new PowerStatusReading("NotPresent", "Adequate", "AC", 100, false),
            DateTimeOffset.UtcNow);

        Assert.AreEqual(BatteryAvailabilityState.NotPresent, result.Availability);
        Assert.IsFalse(result.IsBatteryPresent);
    }

    [TestMethod]
    public void Evaluate_ChargingAndDischargingProduceReliableSnapshots()
    {
        var charging = PowerStatusEvaluator.Evaluate(
            new PowerStatusReading("Charging", "Adequate", "AC", 105, false),
            DateTimeOffset.UtcNow);
        var discharging = PowerStatusEvaluator.Evaluate(
            new PowerStatusReading("Discharging", "NotPresent", "Battery", -4, true),
            DateTimeOffset.UtcNow);

        Assert.IsTrue(charging.IsCharging);
        Assert.AreEqual(100, charging.ChargePercent);
        Assert.IsFalse(discharging.IsCharging);
        Assert.AreEqual(0, discharging.ChargePercent);
        Assert.IsTrue(discharging.IsEnergySaverOn);
    }

    [TestMethod]
    public async Task Service_TransientFailureKeepsLastSuccessfulBatteryAndDisposeStopsResumeCallbacks()
    {
        var resume = new FakeResumeService();
        var reader = new SequenceReader(
            new PowerStatusReading("Discharging", "NotPresent", "Battery", 55, false),
            new InvalidOperationException("temporary"));
        var service = new WindowsPowerStatusService(
            new ImmediateDispatcher(),
            resume,
            reader,
            new FakePowerEventSource());

        await service.StartAsync();
        resume.Raise();

        Assert.AreEqual(BatteryAvailabilityState.TransientError, service.Current.Availability);
        Assert.IsTrue(service.Current.IsBatteryPresent);
        Assert.AreEqual(55, service.Current.ChargePercent);
        var readsBeforeDispose = reader.ReadCount;

        service.Dispose();
        resume.Raise();

        Assert.AreEqual(readsBeforeDispose, reader.ReadCount);
    }

    [TestMethod]
    public async Task Service_AccessFailureDoesNotClaimNoPhysicalBattery()
    {
        var service = new WindowsPowerStatusService(
            new ImmediateDispatcher(),
            null,
            new SequenceReader(new UnauthorizedAccessException()),
            new FakePowerEventSource());

        await service.StartAsync();

        Assert.AreEqual(BatteryAvailabilityState.AccessDenied, service.Current.Availability);
        Assert.IsFalse(service.Current.IsBatteryPresent);
        service.Dispose();
    }

    private sealed class SequenceReader(params object[] values) : IWindowsPowerStatusReader
    {
        private readonly Queue<object> _values = new(values);
        public int ReadCount { get; private set; }
        public PowerStatusReading Read()
        {
            ReadCount++;
            var value = _values.Count > 1 ? _values.Dequeue() : _values.Peek();
            return value is Exception exception ? throw exception : (PowerStatusReading)value;
        }
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public bool HasThreadAccess => true;
        public bool TryEnqueue(Action callback) { callback(); return true; }
    }

    private sealed class FakePowerEventSource : IWindowsPowerEventSource
    {
        public void Subscribe(EventHandler<object> handler) { }
        public void Unsubscribe(EventHandler<object> handler) { }
    }

    private sealed class FakeResumeService : ISystemResumeService
    {
        public event EventHandler? Resumed;
        public void Start() { }
        public void Raise() => Resumed?.Invoke(this, EventArgs.Empty);
        public void Dispose() { }
    }
}
