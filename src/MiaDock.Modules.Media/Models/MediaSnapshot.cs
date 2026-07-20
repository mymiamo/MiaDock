namespace MiaDock.Modules.Media.Models;

public sealed record MediaSnapshot(
    MediaSourceInfo Source,
    TrackInfo Track,
    PlaybackStatus PlaybackStatus,
    TimeSpan Position,
    TimeSpan Duration,
    double Volume,
    MediaCapabilities Capabilities)
{
    public double Progress => Duration <= TimeSpan.Zero
        ? 0
        : Math.Clamp(Position.TotalMilliseconds / Duration.TotalMilliseconds, 0, 1);
}
