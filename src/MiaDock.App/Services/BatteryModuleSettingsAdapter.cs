using MiaDock.Modules.DeviceStatus.Settings;

namespace MiaDock.App.Services;

public sealed class BatteryModuleSettingsAdapter : IBatteryModuleSettings, IDisposable
{
    private readonly ISettingsService _settings;

    public BatteryModuleSettingsAdapter(ISettingsService settings)
    {
        _settings = settings;
        Current = ReadCurrent();
        _settings.SettingsChanged += OnSettingsChanged;
    }

    public BatteryModuleOptions Current { get; private set; }
    public event EventHandler<BatteryModuleOptions>? Changed;

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        var next = ReadCurrent();
        if (next == Current) return;
        Current = next;
        Changed?.Invoke(this, next);
    }

    private BatteryModuleOptions ReadCurrent() => BatteryModuleOptions.FromEnvelope(
        _settings.Current.Modules.TryGetValue("battery", out var envelope) ? envelope : null);

    public void Dispose() => _settings.SettingsChanged -= OnSettingsChanged;
}
