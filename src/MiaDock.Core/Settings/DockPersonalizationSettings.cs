using MiaDock.Core.Presentation;

namespace MiaDock.Core.Settings;

/// <summary>
/// User-facing dock proportions. The optional parent property keeps 1.5.x
/// settings files compatible while defaults retain the existing silhouette.
/// </summary>
public sealed record DockPersonalizationSettings(
    DockSizePreset SizePreset,
    double CustomThickness,
    DockDensity Density,
    DockIconSize IconSize,
    DockCornerStyle CornerStyle,
    AppearancePreset Preset,
    double TopOffset)
{
    public const double MinimumThickness = 30;
    public const double MaximumThickness = 54;
    public const double DefaultThickness = 46;
    public const double MinimumTopOffset = 0;
    public const double MaximumTopOffset = 32;

    public static DockPersonalizationSettings Default { get; } = new(
        DockSizePreset.Default,
        DefaultThickness,
        DockDensity.Balanced,
        DockIconSize.Default,
        DockCornerStyle.DynamicIsland,
        AppearancePreset.Dynamic,
        12);
}

public enum DockSizePreset
{
    Compact,
    Default,
    Large,
    Custom
}

public enum DockDensity
{
    Compact,
    Balanced,
    Spacious
}

public enum DockIconSize
{
    Small,
    Default,
    Large
}

public enum AppearancePreset
{
    Minimal,
    Dynamic,
    Large,
    Custom
}
