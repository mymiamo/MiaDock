namespace MiaDock.Modules.Media.Models;

public readonly record struct TrackIdentity(
    string SourceId,
    string Title,
    string Artist,
    string AlbumTitle)
{
    public static TrackIdentity? From(MediaSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!snapshot.HasMedia)
        {
            return null;
        }

        return new TrackIdentity(
            Normalize(snapshot.Source.Id),
            Normalize(snapshot.Track.Title),
            Normalize(snapshot.Track.Artist),
            Normalize(snapshot.Track.AlbumTitle));
    }

    private static string Normalize(string? value) => (value ?? string.Empty).Trim();
}
