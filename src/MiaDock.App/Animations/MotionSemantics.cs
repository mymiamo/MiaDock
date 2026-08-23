using MiaDock.Core.Presentation;

namespace MiaDock.App.Animations;

/// <summary>
/// Resolves optional motion affordances centrally so island and module events
/// follow one consistent accessibility contract.
/// </summary>
internal static class MotionSemantics
{
    public static bool UsesEventMorph(IslandTransition transition, IslandMotionOptions options) =>
        options.EnableEventMorph && IslandAnimationProfile.IsEventMorph(transition);

    public static bool UsesShellPulse(IslandMotionOptions options) =>
        options.EnableIconAnimations && options.Preset != MotionPreset.Off;
}
