using MiaDock.Modules.SystemStatus;
using MiaDock.Modules.SystemStatus.Settings;

namespace MiaDock.App.Services;

public sealed class VolumeModuleSettingsAdapter : IVolumeModuleSettings, IDisposable
{
    private readonly ISettingsService _settings;

    public VolumeModuleSettingsAdapter(ISettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Current = ReadCurrent();
        _settings.SettingsChanged += OnSettingsChanged;
    }

    public VolumeModuleOptions Current { get; private set; }

    public event EventHandler<VolumeModuleOptions>? Changed;

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        var next = ReadCurrent();
        if (next == Current)
        {
            return;
        }

        Current = next;
        Changed?.Invoke(this, next);
    }

    private VolumeModuleOptions ReadCurrent() => VolumeModuleOptions.FromEnvelope(
        _settings.Current.Modules.TryGetValue(VolumeModule.ModuleId, out var envelope)
            ? envelope
            : null);

    public void Dispose() => _settings.SettingsChanged -= OnSettingsChanged;
}
