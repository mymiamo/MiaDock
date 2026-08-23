namespace MiaDock.Modules.Media.Models;

public enum MediaAudioLevelSource
{
    Unavailable,
    SessionPeak,
    EndpointSpectrum,
    ProcessSpectrum
}

/// <summary>
/// A compact audio visualization sample. Frequency bands are meaningful only
/// when <see cref="HasFrequencySpectrum"/> is true; session peaks remain
/// channel amplitude and are never relabelled as bass, vocal, or treble.
/// </summary>
public sealed record MediaAudioLevelSnapshot(
    MediaAudioLevelSource Source,
    double Treble,
    double Bass,
    double Vocal,
    double LeftAmplitude,
    double RightAmplitude)
{
    public bool IsAvailable => Source != MediaAudioLevelSource.Unavailable;

    public bool HasFrequencySpectrum => Source is
        MediaAudioLevelSource.EndpointSpectrum or MediaAudioLevelSource.ProcessSpectrum;

    public double Amplitude => Math.Max(LeftAmplitude, RightAmplitude);

    public static MediaAudioLevelSnapshot Silent { get; } = new(
        MediaAudioLevelSource.Unavailable,
        0.18,
        0.18,
        0.18,
        0,
        0);

    public static MediaAudioLevelSnapshot FromSessionPeak(
        double leftAmplitude,
        double rightAmplitude) => new(
            MediaAudioLevelSource.SessionPeak,
            0.18,
            0.18,
            0.18,
            Math.Clamp(leftAmplitude, 0, 1),
            Math.Clamp(rightAmplitude, 0, 1));
}
