namespace MiaDock.Modules.Media.Models;

public sealed record MediaAudioLevelSnapshot(
    bool IsAvailable,
    double Left,
    double Center,
    double Right)
{
    public static MediaAudioLevelSnapshot Silent { get; } = new(false, 0.18, 0.18, 0.18);
}
