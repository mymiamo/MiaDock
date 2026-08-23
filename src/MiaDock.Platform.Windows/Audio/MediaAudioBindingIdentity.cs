using MiaDock.Modules.Media.Models;

namespace MiaDock.Platform.Windows.Audio;

internal readonly record struct MediaAudioBindingIdentity(
    string SourceId,
    bool HasMedia,
    bool IsPlaying)
{
    public static MediaAudioBindingIdentity From(MediaSnapshot snapshot) =>
        new(
            snapshot.Source.Id,
            snapshot.HasMedia,
            snapshot.PlaybackStatus == PlaybackStatus.Playing);
}
