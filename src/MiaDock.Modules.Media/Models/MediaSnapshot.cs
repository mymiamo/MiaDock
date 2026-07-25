namespace MiaDock.Modules.Media.Models;

public sealed record MediaSnapshot(
    MediaSourceInfo Source,
    TrackInfo Track,
    PlaybackStatus PlaybackStatus,
    double PlaybackRate,
    TimeSpan Position,
    TimeSpan Duration,
    double Volume,
    MediaCapabilities Capabilities)
{
    public long Sequence { get; init; }

    public long TrackRevision { get; init; }

    public static MediaSnapshot Empty { get; } = new(
        new MediaSourceInfo(string.Empty, "No media source", null),
        new TrackInfo("No media playing", string.Empty, string.Empty, null),
        PlaybackStatus.Stopped,
        1,
        TimeSpan.Zero,
        TimeSpan.Zero,
        0,
        new MediaCapabilities(false, false, false, false, false, false));

    public bool HasMedia => !string.IsNullOrWhiteSpace(Source.Id);

    public double Progress => Duration <= TimeSpan.Zero
        ? 0
        : Math.Clamp(Position.TotalMilliseconds / Duration.TotalMilliseconds, 0, 1);
}
