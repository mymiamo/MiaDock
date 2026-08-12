namespace MiaDock.App.Animations;

/// <summary>
/// Easing used by <see cref="IslandBoundsAnimator"/> while interpolating
/// width, height and corner radii together.
/// </summary>
public enum BoundsEasingKind
{
    /// <summary>Standard cubic ease-out for hover/expand/collapse.</summary>
    CubicOut = 0,

    /// <summary>
    /// Soft spring/back ease-out for Compact↔Notification morphs.
    /// Overshoot stays mild and is scaled by springiness.
    /// </summary>
    SoftSpringOut = 1
}

public readonly record struct BoundsEasingProfile(BoundsEasingKind Kind, double Springiness)
{
    public static BoundsEasingProfile Cubic { get; } = new(BoundsEasingKind.CubicOut, 0);

    public static BoundsEasingProfile SoftSpring(double springiness) =>
        new(BoundsEasingKind.SoftSpringOut, Math.Clamp(springiness, 0, 1));
}
