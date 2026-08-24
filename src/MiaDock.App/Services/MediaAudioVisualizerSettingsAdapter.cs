using MiaDock.Modules.Media.Services;

namespace MiaDock.App.Services;

public sealed class MediaAudioVisualizerSettingsAdapter : IMediaAudioVisualizerSettings, IDisposable
{
    private readonly ISettingsService _settings;

    public MediaAudioVisualizerSettingsAdapter(ISettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ExperimentalSpectrumEnabled = settings.Current.Media.ExperimentalSpectrumEnabled;
        SpectrumOutputDeviceId = settings.Current.Media.SpectrumOutputDeviceId;
        _settings.SettingsChanged += OnSettingsChanged;
    }

    public bool ExperimentalSpectrumEnabled { get; private set; }
    public string? SpectrumOutputDeviceId { get; private set; }

    public event EventHandler<bool>? ExperimentalSpectrumEnabledChanged;
    public event EventHandler<string?>? SpectrumOutputDeviceIdChanged;

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        var enabled = args.Current.Media.ExperimentalSpectrumEnabled;
        if (enabled != ExperimentalSpectrumEnabled)
        {
            ExperimentalSpectrumEnabled = enabled;
            ExperimentalSpectrumEnabledChanged?.Invoke(this, enabled);
        }

        var outputDeviceId = args.Current.Media.SpectrumOutputDeviceId;
        if (!string.Equals(outputDeviceId, SpectrumOutputDeviceId, StringComparison.Ordinal))
        {
            SpectrumOutputDeviceId = outputDeviceId;
            SpectrumOutputDeviceIdChanged?.Invoke(this, outputDeviceId);
        }
    }

    public void Dispose() => _settings.SettingsChanged -= OnSettingsChanged;
}
