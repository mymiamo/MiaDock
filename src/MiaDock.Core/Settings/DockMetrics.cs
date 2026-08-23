using MiaDock.Core.Presentation;

namespace MiaDock.Core.Settings;

/// <summary>
/// Resolves every state dimension from one semantic appearance model. Views
/// consume the resulting layout rather than each interpreting size settings.
/// </summary>
public static class DockMetrics
{
    public static IslandLayoutOptions ToLayout(AppearanceSettings appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        var personalization = appearance.EffectivePersonalization;
        // Size presets own proportions, not the user's individually editable
        // corners. Carry the four radii into every baseline so selecting a
        // different dock size cannot silently replace them with a uniform one.
        var cornerRadii = appearance.EffectiveCornerRadii;
        var baseline = personalization.SizePreset switch
        {
            DockSizePreset.Compact => new IslandLayoutOptions(260, 38, 276, 64, 500, 334, 400, 82, 19, cornerRadii),
            DockSizePreset.Large => new IslandLayoutOptions(330, 54, 340, 82, 600, 390, 480, 108, 27, cornerRadii),
            _ => new IslandLayoutOptions(
                appearance.CollapsedWidth,
                appearance.CollapsedHeight,
                appearance.HoverWidth,
                appearance.HoverHeight,
                appearance.ExpandedWidth,
                appearance.ExpandedHeight,
                appearance.NotificationWidth,
                appearance.NotificationHeight,
                appearance.CornerRadius,
                cornerRadii)
        };

        var thicknessScale = personalization.SizePreset == DockSizePreset.Custom
            ? Math.Clamp(
                personalization.CustomThickness,
                DockPersonalizationSettings.MinimumThickness,
                DockPersonalizationSettings.MaximumThickness) /
              DockPersonalizationSettings.DefaultThickness
            : 1d;
        var densityScale = personalization.Density switch
        {
            DockDensity.Compact => 0.92,
            DockDensity.Spacious => 1.08,
            _ => 1d
        };
        var iconScale = personalization.IconSize switch
        {
            DockIconSize.Small => 0.9,
            DockIconSize.Large => 1.1,
            _ => 1d
        };

        return baseline with
        {
            CollapsedWidth = ClampWidth(baseline.CollapsedWidth * densityScale, 120, 360),
            CollapsedHeight = ClampHeight(baseline.CollapsedHeight * thicknessScale, 30, 96),
            HoverWidth = ClampWidth(baseline.HoverWidth * densityScale, 180, 520),
            HoverHeight = ClampHeight(baseline.HoverHeight * thicknessScale, 48, 160),
            ExpandedWidth = ClampWidth(baseline.ExpandedWidth * densityScale, 480, 720),
            ExpandedHeight = ClampHeight(baseline.ExpandedHeight * thicknessScale, 300, 420),
            NotificationWidth = ClampWidth(baseline.NotificationWidth * densityScale, 240, 620),
            NotificationHeight = ClampHeight(baseline.NotificationHeight * thicknessScale, 64, 180),
            CornerStyle = personalization.CornerStyle,
            ContentScale = iconScale
        };
    }

    public static AppearanceSettings ApplyPreset(AppearanceSettings appearance, AppearancePreset preset)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        var current = appearance.EffectivePersonalization;
        var personalization = preset switch
        {
            AppearancePreset.Minimal => current with
            {
                Preset = preset,
                SizePreset = DockSizePreset.Compact,
                Density = DockDensity.Compact,
                IconSize = DockIconSize.Small
            },
            AppearancePreset.Large => current with
            {
                Preset = preset,
                SizePreset = DockSizePreset.Large,
                Density = DockDensity.Spacious,
                IconSize = DockIconSize.Large
            },
            AppearancePreset.Dynamic => current with
            {
                Preset = preset,
                SizePreset = DockSizePreset.Default,
                Density = DockDensity.Balanced,
                IconSize = DockIconSize.Default
            },
            _ => current with { Preset = AppearancePreset.Custom }
        };
        return appearance with { Personalization = personalization };
    }

    private static double ClampWidth(double value, double minimum, double maximum) =>
        Math.Clamp(Math.Round(value), minimum, maximum);

    private static double ClampHeight(double value, double minimum, double maximum) =>
        Math.Clamp(Math.Round(value), minimum, maximum);
}
