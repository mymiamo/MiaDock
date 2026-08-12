using MiaDock.Core.Presentation;

namespace MiaDock.Core.Settings;

public sealed record MotionSettings(
    MotionPreset Preset,
    double Speed,
    double Intensity,
    double Springiness,
    int ContentDelayMilliseconds,
    bool EnableParallax,
    bool EnableTransientBlur)
{
    public static MotionSettings Default { get; } = new(
        MotionPreset.Balanced,
        1,
        0.7,
        0.55,
        48,
        false,
        false);

    public static MotionSettings FromLegacy(
        IslandAnimationKind animationKind,
        double speed) => new(
            animationKind switch
            {
                IslandAnimationKind.ScaleFade => MotionPreset.Balanced,
                IslandAnimationKind.SlideFade => MotionPreset.Fluid,
                IslandAnimationKind.Spring => MotionPreset.Springy,
                _ => MotionPreset.Balanced
            },
            speed,
            MotionSettings.Default.Intensity,
            MotionSettings.Default.Springiness,
            MotionSettings.Default.ContentDelayMilliseconds,
            false,
            false);
}
