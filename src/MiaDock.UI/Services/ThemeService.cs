using Microsoft.UI.Xaml;
using MiaDock.Core.Theming;
using MiaDock.Core.Settings;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace MiaDock.UI.Services;

public sealed class ThemeService : IThemeService
{
    private ResourceDictionary? _styleDictionary;
    private ResourceDictionary? _customDictionary;

    public ThemeStyle CurrentStyle { get; private set; } = ThemeStyle.AppleLike;

    public void Apply(ThemeStyle style)
    {
        var resources = Application.Current.Resources.MergedDictionaries;

        if (_styleDictionary is not null)
        {
            resources.Remove(_styleDictionary);
        }

        if (_customDictionary is not null)
        {
            resources.Remove(_customDictionary);
        }

        _styleDictionary = new ResourceDictionary
        {
            Source = new Uri($"ms-appx:///Themes/{GetFileName(style)}")
        };
        _customDictionary = new ResourceDictionary();

        resources.Add(_styleDictionary);
        resources.Add(_customDictionary);
        CurrentStyle = style;
    }

    public void Apply(AppearanceSettings appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        Apply(appearance.Theme);
        var surfaceColor = ParseColor(appearance.BackgroundColor);
        var accentColor = ParseColor(appearance.AccentColor);
        _customDictionary!["IslandStyleSurfaceBrush"] = CreateSurfaceBrush(
            appearance.Theme,
            surfaceColor,
            appearance.Opacity);
        if (appearance.Theme is ThemeStyle.AppleLike or ThemeStyle.CustomSolidColor)
        {
            ApplyAdaptiveSolidPalette(surfaceColor, accentColor);
            return;
        }

        _customDictionary["IslandStyleControlBrush"] = new SolidColorBrush(Color.FromArgb(
            appearance.Theme == ThemeStyle.BlurredGlass ? (byte)0x20 : (byte)0x38,
            accentColor.R,
            accentColor.G,
            accentColor.B));
        _customDictionary["IslandStyleAccentBrush"] = new SolidColorBrush(accentColor);
    }

    private static string GetFileName(ThemeStyle style) => style switch
    {
        ThemeStyle.AppleLike => "AppleLikeTheme.xaml",
        ThemeStyle.Windows11Mica or
        ThemeStyle.Windows11MicaAlt or
        ThemeStyle.Windows11Acrylic or
        ThemeStyle.Windows11AcrylicThin => "Windows11Theme.xaml",
        ThemeStyle.BlurredGlass => "BlurredGlassTheme.xaml",
        ThemeStyle.CustomSolidColor => "AppleLikeTheme.xaml",
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, null)
    };

    private static Brush CreateSurfaceBrush(ThemeStyle style, Color color, double opacity)
    {
        var normalizedOpacity = Math.Clamp(opacity, 0.35, 1);
        return style switch
        {
            ThemeStyle.Windows11Mica => new SolidColorBrush(WithOpacity(color, 0.91 * normalizedOpacity)),
            ThemeStyle.Windows11MicaAlt => new SolidColorBrush(WithOpacity(color, 0.86 * normalizedOpacity)),
            ThemeStyle.Windows11Acrylic => CreateAcrylic(color, 0.72, normalizedOpacity),
            ThemeStyle.Windows11AcrylicThin => CreateAcrylic(color, 0.46, normalizedOpacity),
            ThemeStyle.BlurredGlass => CreateGlassOverlay(normalizedOpacity),
            _ => new SolidColorBrush(WithOpacity(color, normalizedOpacity))
        };
    }

    private static SolidColorBrush CreateGlassOverlay(double opacity) => new(Color.FromArgb(
        checked((byte)Math.Round(0.08 * opacity * byte.MaxValue)),
        0x14,
        0x14,
        0x14));

    private static AcrylicBrush CreateAcrylic(Color color, double tintOpacity, double opacity) => new()
    {
        FallbackColor = color,
        TintColor = color,
        TintOpacity = tintOpacity,
        Opacity = opacity
    };

    private static Color WithOpacity(Color color, double opacity) => Color.FromArgb(
        checked((byte)Math.Round(Math.Clamp(opacity, 0, 1) * byte.MaxValue)),
        color.R,
        color.G,
        color.B);

    private void ApplyAdaptiveSolidPalette(Color surfaceColor, Color requestedAccent)
    {
        var palette = SolidThemeContrastPaletteFactory.Create(surfaceColor, requestedAccent);
        _customDictionary!["IslandSurfaceBrush"] = new SolidColorBrush(surfaceColor);
        _customDictionary["IslandSurfaceSecondaryBrush"] = new SolidColorBrush(palette.Control);
        _customDictionary["IslandTextPrimaryBrush"] = new SolidColorBrush(palette.Primary);
        _customDictionary["IslandTextSecondaryBrush"] = new SolidColorBrush(palette.Secondary);
        _customDictionary["IslandAccentBrush"] = new SolidColorBrush(palette.Accent);
        _customDictionary["IslandStrokeBrush"] = new SolidColorBrush(palette.Stroke);
        _customDictionary["IslandControlFillBrush"] = new SolidColorBrush(Color.FromArgb(
            38,
            palette.Primary.R,
            palette.Primary.G,
            palette.Primary.B));
        _customDictionary["IslandStyleControlBrush"] = new SolidColorBrush(palette.Control);
        _customDictionary["IslandStyleAccentBrush"] = new SolidColorBrush(palette.Accent);
        _customDictionary["IslandIconButtonRestBrush"] = new SolidColorBrush(
            Color.FromArgb(0, 0, 0, 0));
        _customDictionary["IslandIconButtonPointerOverBrush"] = new SolidColorBrush(Color.FromArgb(
            40,
            palette.Primary.R,
            palette.Primary.G,
            palette.Primary.B));
        _customDictionary["IslandIconButtonPressedBrush"] = new SolidColorBrush(Color.FromArgb(
            66,
            palette.Primary.R,
            palette.Primary.G,
            palette.Primary.B));
        _customDictionary["IslandIconButtonForegroundBrush"] = new SolidColorBrush(palette.Primary);
    }

    private static Color ParseColor(string value)
    {
        var normalized = value.TrimStart('#');
        return Color.FromArgb(
            255,
            Convert.ToByte(normalized[0..2], 16),
            Convert.ToByte(normalized[2..4], 16),
            Convert.ToByte(normalized[4..6], 16));
    }
}
