namespace MiaDock.Core.Presentation;

public sealed record IslandLayoutOptions(
    double CollapsedWidth,
    double CollapsedHeight,
    double HoverWidth,
    double HoverHeight,
    double ExpandedWidth,
    double ExpandedHeight,
    double NotificationWidth,
    double NotificationHeight,
    double CornerRadius,
    DockCornerRadii? CornerRadii = null)
{
    public static IslandLayoutOptions Default { get; } = new(
        292, 46,
        300, 72,
        548, 360,
        440, 92,
        23,
        DockCornerRadii.Uniform(23));

    public DockCornerRadii EffectiveCornerRadii =>
        CornerRadii ?? DockCornerRadii.Uniform(CornerRadius);
}
