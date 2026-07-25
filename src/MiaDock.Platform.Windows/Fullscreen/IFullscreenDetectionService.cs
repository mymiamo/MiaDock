namespace MiaDock.Platform.Windows.Fullscreen;

public interface IFullscreenDetectionService : IDisposable
{
    FullscreenSnapshot Current { get; }

    Exception? LastFailure { get; }

    event EventHandler<FullscreenSnapshot>? StateChanged;

    void Start();

    void Refresh();
}
