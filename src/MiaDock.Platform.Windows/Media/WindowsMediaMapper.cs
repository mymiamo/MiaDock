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
        var playbackInfo = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();
        var mediaProperties = await session.TryGetMediaPropertiesAsync().AsTask(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var title = string.IsNullOrWhiteSpace(mediaProperties.Title)
            ? "Unknown media"
            : mediaProperties.Title;
        var artist = !string.IsNullOrWhiteSpace(mediaProperties.Artist)
            ? mediaProperties.Artist
            : mediaProperties.AlbumArtist ?? string.Empty;
        var albumTitle = mediaProperties.AlbumTitle ?? string.Empty;
        var artwork = includeArtwork
            ? await _imageReader.ReadAsync(
                mediaProperties.Thumbnail,
                CreateArtworkCacheKey(source.Id, title, artist, albumTitle, artworkRevision),
                cancellationToken).ConfigureAwait(false)
            : null;

        var start = timeline.StartTime;
        var duration = timeline.EndTime > start ? timeline.EndTime - start : TimeSpan.Zero;
        var position = timeline.Position > start ? timeline.Position - start : TimeSpan.Zero;
        position = duration > TimeSpan.Zero && position > duration ? duration : position;
        var controls = playbackInfo.Controls;

        return new MediaSnapshot(
            source,
            new TrackInfo(title, artist, albumTitle, artwork),
            MapPlaybackStatus(playbackInfo.PlaybackStatus),
            playbackInfo.PlaybackRate is > 0 ? playbackInfo.PlaybackRate.Value : 1,
            position,
            duration,
            0,
            new MediaCapabilities(
                controls.IsPlayEnabled || controls.IsPlayPauseToggleEnabled,
                controls.IsPauseEnabled || controls.IsPlayPauseToggleEnabled,
                controls.IsPreviousEnabled,
                controls.IsNextEnabled,
                controls.IsPlaybackPositionEnabled,
                false));
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
