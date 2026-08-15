using MiaDock.Modules.DeviceStatus.Settings;

namespace MiaDock.App.Services;

public sealed class DeviceHubSettingsAdapter : IDeviceHubSettings, IDisposable
{
    private readonly ISettingsService _settings;

    public DeviceHubSettingsAdapter(ISettingsService settings)
    {
        _settings = settings;
        Current = ReadCurrent();
        _settings.SettingsChanged += OnSettingsChanged;
    }

    public DeviceHubOptions Current { get; private set; }
    public event EventHandler<DeviceHubOptions>? Changed;

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        var next = ReadCurrent();
        if (next == Current) return;
        Current = next;
        Changed?.Invoke(this, next);
    }

    private DeviceHubOptions ReadCurrent() => DeviceHubOptions.FromEnvelope(
        _settings.Current.Modules.TryGetValue("device-hub", out var envelope) ? envelope : null);

    public void Dispose() => _settings.SettingsChanged -= OnSettingsChanged;
}
