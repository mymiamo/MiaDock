using MiaDock.Modules.Media.Models;

namespace MiaDock.Platform.Windows.Audio;

internal readonly record struct MediaAudioBindingIdentity(string SourceId, bool HasMedia)
{
    public static MediaAudioBindingIdentity From(MediaSnapshot snapshot) =>
        new(snapshot.Source.Id, snapshot.HasMedia);
}
