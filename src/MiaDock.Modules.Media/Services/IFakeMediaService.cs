using MiaDock.Modules.Media.Models;

namespace MiaDock.Modules.Media.Services;

public interface IFakeMediaService
{
    event EventHandler<MediaSnapshot>? SnapshotChanged;

    IReadOnlyList<MediaScenario> Scenarios { get; }

    MediaSnapshot Current { get; }

    void SelectScenario(string scenarioId);

    void TogglePlayback();

    void SkipPrevious();

    void SkipNext();

    void Seek(double progress);

    void SetVolume(double volume);
}
