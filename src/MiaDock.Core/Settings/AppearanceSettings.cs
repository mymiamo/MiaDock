using MiaDock.Core.Presentation;
using MiaDock.Core.Theming;

namespace MiaDock.Core.Settings;

public sealed record AppearanceSettings(
    ThemeStyle Theme,
    double CollapsedWidth,
    double CollapsedHeight,
    double HoverWidth,
    double HoverHeight,
    double ExpandedWidth,
    double ExpandedHeight,
    double NotificationWidth,
    double NotificationHeight,
    double CornerRadius,
    string BackgroundColor,
    string AccentColor,
    double Opacity,
    double ShadowIntensity,
    double AnimationSpeed,
    IslandAnimationKind AnimationKind,
    MotionSettings? Motion = null,
    double EdgeMargin = 12,
    DockCornerRadii? CornerRadii = null,
    bool LinkCornerRadii = true)
{
    public static AppearanceSettings Default { get; } = new(
        ThemeStyle.AppleLike,
        292, 46,
        300, 72,
        548, 360,
        440, 92,
        23,
        "#000000",
        "#FFFFFF",
        1,
        0,
        1,
        IslandAnimationKind.ScaleFade,
        MotionSettings.Default,
        12,
        DockCornerRadii.Uniform(23),
        true);

    public DockCornerRadii EffectiveCornerRadii =>
        CornerRadii ?? DockCornerRadii.Uniform(CornerRadius);
}
