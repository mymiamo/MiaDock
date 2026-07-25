using Microsoft.Windows.System.Power;
using MiaDock.Core.Threading;
using MiaDock.Core.Logging;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;

namespace MiaDock.Platform.Windows.Power;

public sealed class WindowsPowerStatusService : IPowerStatusService
{
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogService? _log;
    private bool _started;
    private bool _disposed;

    public WindowsPowerStatusService(IUiDispatcher dispatcher, ILogService? log = null)
    {
        _dispatcher = dispatcher;
        _log = log;
    }

    public BatteryStatusSnapshot Current { get; private set; } = BatteryStatusSnapshot.Default;

    public event EventHandler<BatteryStatusSnapshot>? SnapshotChanged;

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started) return ValueTask.CompletedTask;

        _started = true;
        Current = Current with { State = DeviceServiceState.Starting };
        try
        {
            PowerManager.BatteryStatusChanged += OnPowerChanged;
            PowerManager.PowerSupplyStatusChanged += OnPowerChanged;
            PowerManager.PowerSourceKindChanged += OnPowerChanged;
            PowerManager.RemainingChargePercentChanged += OnPowerChanged;
            PowerManager.EnergySaverStatusChanged += OnPowerChanged;
            var snapshot = ReadSnapshot();
            Publish(snapshot);
            _log?.Write(TechnicalLogLevel.Information, TechnicalEventIds.PowerStatusReady,
                "DeviceStatus", "Power status service initialized.", properties: new Dictionary<string, object?>
                {
                    ["state"] = snapshot.State.ToString(),
                    ["batteryPresent"] = snapshot.IsBatteryPresent
                });
        }
        catch (Exception)
        {
            Unsubscribe();
            _started = false;
            Publish(BatteryStatusSnapshot.Default with { State = DeviceServiceState.Unavailable });
            _log?.Write(TechnicalLogLevel.Warning, TechnicalEventIds.DeviceStatusUnavailable,
                "DeviceStatus", "Power status service is unavailable.", properties: new Dictionary<string, object?> { ["service"] = "power" });
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_started) return ValueTask.CompletedTask;
        Unsubscribe();
        _started = false;
        Publish(BatteryStatusSnapshot.Default);
        return ValueTask.CompletedTask;
    }

    private void OnPowerChanged(object? sender, object args)
    {
        try { Publish(ReadSnapshot()); }
        catch (Exception) { Publish(Current with { State = DeviceServiceState.Faulted }); }
    }

    private static BatteryStatusSnapshot ReadSnapshot()
    {
        var batteryStatus = PowerManager.BatteryStatus.ToString();
        var supplyStatus = PowerManager.PowerSupplyStatus.ToString();
        var source = PowerManager.PowerSourceKind.ToString();
        var present = !batteryStatus.Equals("NotPresent", StringComparison.OrdinalIgnoreCase) &&
                      !supplyStatus.Equals("NotPresent", StringComparison.OrdinalIgnoreCase);
        var charging = batteryStatus.Equals("Charging", StringComparison.OrdinalIgnoreCase);
        var saver = PowerManager.EnergySaverStatus.ToString().Equals("On", StringComparison.OrdinalIgnoreCase);
        var sourceText = source switch
        {
            "AC" => "Prize bağlı",
            "Battery" => "Pil gücü",
            "USB" => "USB güç kaynağı",
            "Wireless" => "Kablosuz güç",
            _ => "Güç kaynağı bilinmiyor"
        };

        return new BatteryStatusSnapshot(
            DeviceServiceState.Ready,
            present,
            present ? Math.Clamp(PowerManager.RemainingChargePercent, 0, 100) : 0,
            charging,
            saver,
            sourceText);
    }

    private void Publish(BatteryStatusSnapshot snapshot)
    {
        void Apply()
        {
            Current = snapshot;
            SnapshotChanged?.Invoke(this, snapshot);
        }

        if (_dispatcher.HasThreadAccess) Apply(); else _dispatcher.TryEnqueue(Apply);
    }

    private void Unsubscribe()
    {
        PowerManager.BatteryStatusChanged -= OnPowerChanged;
        PowerManager.PowerSupplyStatusChanged -= OnPowerChanged;
        PowerManager.PowerSourceKindChanged -= OnPowerChanged;
        PowerManager.RemainingChargePercentChanged -= OnPowerChanged;
        PowerManager.EnergySaverStatusChanged -= OnPowerChanged;
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_started) Unsubscribe();
        _started = false;
        _disposed = true;
    }
}
