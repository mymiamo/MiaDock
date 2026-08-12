using System.Security.Cryptography;
using System.Text;
using Windows.Media.Control;
using MiaDock.Modules.Media.Models;
using WindowsPlaybackStatus = Windows.Media.Control.GlobalSystemMediaTransportControlsSessionPlaybackStatus;

namespace MiaDock.Platform.Windows.Media;

internal sealed class WindowsMediaMapper
{
    private readonly MediaImageReader _imageReader;

    public WindowsMediaMapper(MediaImageReader imageReader)
    {
        _imageReader = imageReader;
    }

    public async Task<MediaSnapshot> MapAsync(
        GlobalSystemMediaTransportControlsSession session,
        MediaSourceInfo source,
        CancellationToken cancellationToken,
        bool includeArtwork = true,
        long artworkRevision = 0)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var playbackInfo = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();
        var playbackStatus = MapPlaybackStatus(playbackInfo.PlaybackStatus);
        var playbackRate = playbackInfo.PlaybackRate is > 0 ? playbackInfo.PlaybackRate.Value : 1;
        var controls = playbackInfo.Controls;
        var capabilities = new MediaCapabilities(
            controls.IsPlayEnabled || controls.IsPlayPauseToggleEnabled,
            controls.IsPauseEnabled || controls.IsPlayPauseToggleEnabled,
            controls.IsPreviousEnabled,
            controls.IsNextEnabled,
            controls.IsPlaybackPositionEnabled,
            false);
        var start = timeline.StartTime;
        var end = timeline.EndTime;
        var rawPosition = timeline.Position;

        var mediaProperties = await session.TryGetMediaPropertiesAsync().AsTask(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var title = string.IsNullOrWhiteSpace(mediaProperties.Title)
            ? "Unknown media"
            : mediaProperties.Title;
        var artist = !string.IsNullOrWhiteSpace(mediaProperties.Artist)
            ? mediaProperties.Artist
            : mediaProperties.AlbumArtist ?? string.Empty;
        var albumTitle = mediaProperties.AlbumTitle ?? string.Empty;
        var thumbnailReference = mediaProperties.Thumbnail;
        var artwork = includeArtwork
            ? await _imageReader.ReadAsync(
                thumbnailReference,
                CreateArtworkCacheKey(source.Id, title, artist, albumTitle, artworkRevision),
                cancellationToken).ConfigureAwait(false)
            : null;
        cancellationToken.ThrowIfCancellationRequested();

        var duration = end > start ? end - start : TimeSpan.Zero;
        var position = rawPosition > start ? rawPosition - start : TimeSpan.Zero;
        position = duration > TimeSpan.Zero && position > duration ? duration : position;

        return new MediaSnapshot(
            source,
            new TrackInfo(title, artist, albumTitle, artwork),
            playbackStatus,
            playbackRate,
            position,
            duration,
            0,
            capabilities);
    }

    internal static PlaybackStatus MapPlaybackStatus(WindowsPlaybackStatus status) => status switch
    {
        WindowsPlaybackStatus.Playing => PlaybackStatus.Playing,
        WindowsPlaybackStatus.Paused => PlaybackStatus.Paused,
        _ => PlaybackStatus.Stopped
    };

    private static string CreateArtworkCacheKey(
        string sourceId,
        string title,
        string artist,
        string albumTitle,
        long artworkRevision)
    {
        var identity = $"{sourceId}\n{title}\n{artist}\n{albumTitle}\n{artworkRevision}";
        return $"artwork:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))}";
    }
}
