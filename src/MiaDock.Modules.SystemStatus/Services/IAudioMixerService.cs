using MiaDock.Modules.SystemStatus.Models;

namespace MiaDock.Modules.SystemStatus.Services;

public interface IAudioMixerService
{
    AudioMixerSnapshot CurrentMixer { get; }

    event EventHandler<AudioMixerSnapshot>? MixerChanged;

    Task StartAsync(CancellationToken cancellationToken = default);

    void SetMeteringEnabled(bool enabled);

    Task<bool> SetSessionVolumeAsync(
        string sessionKey,
        double volume,
        CancellationToken cancellationToken = default);

    Task<bool> ToggleSessionMuteAsync(
        string sessionKey,
        CancellationToken cancellationToken = default);
}
