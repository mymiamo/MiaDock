namespace MiaDock.Modules.Media.Services;

public interface IMediaAudioVisualizerSettings
{
    bool ExperimentalSpectrumEnabled { get; }
    string? SpectrumOutputDeviceId { get; }

    event EventHandler<bool>? ExperimentalSpectrumEnabledChanged;
    event EventHandler<string?>? SpectrumOutputDeviceIdChanged;
}
