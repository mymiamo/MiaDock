using MiaDock.Modules.Media.Models;

namespace MiaDock.Modules.Media.Services;

public interface IMediaAudioMeterService : IDisposable
{
    MediaAudioLevelSnapshot Current { get; }

    event EventHandler<MediaAudioLevelSnapshot>? LevelChanged;

    void SetActive(bool active);
}
