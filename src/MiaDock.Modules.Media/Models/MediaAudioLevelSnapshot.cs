namespace MiaDock.Modules.Media.Models;

public sealed record MediaAudioLevelSnapshot(
    bool IsAvailable,
    double Left,
    double Center,
    double Right)
{
    public double Treble => Left;

    public double Bass => Center;

    public double Vocal => Right;

    public static MediaAudioLevelSnapshot Silent { get; } = new(false, 0.18, 0.18, 0.18);
}
