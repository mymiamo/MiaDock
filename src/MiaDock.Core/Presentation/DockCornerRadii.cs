namespace MiaDock.Core.Presentation;

public readonly record struct DockCornerRadii(
    double TopLeft,
    double TopRight,
    double BottomRight,
    double BottomLeft)
{
    public static DockCornerRadii Uniform(double radius) =>
        new(radius, radius, radius, radius);

    public bool IsUniform(double tolerance = 0.01) =>
        Math.Abs(TopLeft - TopRight) < tolerance &&
        Math.Abs(TopLeft - BottomRight) < tolerance &&
        Math.Abs(TopLeft - BottomLeft) < tolerance;

    public DockCornerRadii Clamp(double minimum, double maximum) => new(
        Math.Clamp(TopLeft, minimum, maximum),
        Math.Clamp(TopRight, minimum, maximum),
        Math.Clamp(BottomRight, minimum, maximum),
        Math.Clamp(BottomLeft, minimum, maximum));

    public DockCornerRadii Scale(double factor) => new(
        TopLeft * factor,
        TopRight * factor,
        BottomRight * factor,
        BottomLeft * factor);
}
