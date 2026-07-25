using MiaDock.Modules.Media.Models;

namespace MiaDock.Modules.Media.Services;

public interface IMediaSessionService : IAsyncDisposable
{
    event EventHandler<MediaSnapshot>? SnapshotChanged;

    event EventHandler<IReadOnlyList<MediaSourceInfo>>? SourcesChanged;

    event EventHandler<MediaServiceState>? StateChanged;

    MediaServiceState State { get; }

    IReadOnlyList<MediaSourceInfo> Sources { get; }

    MediaSnapshot Current { get; }

    MediaSelectionOptions Selection { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task SetSelectionAsync(
        MediaSelectionOptions selection,
        CancellationToken cancellationToken = default);

    Task<bool> TogglePlaybackAsync(CancellationToken cancellationToken = default);

    Task<bool> SkipPreviousAsync(CancellationToken cancellationToken = default);

    Task<bool> SkipNextAsync(CancellationToken cancellationToken = default);

    Task<bool> SeekAsync(TimeSpan position, CancellationToken cancellationToken = default);
}
