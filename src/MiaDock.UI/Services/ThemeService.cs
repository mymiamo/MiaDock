using Microsoft.UI.Xaml;
using MiaDock.Core.Theming;
using MiaDock.Core.Settings;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Microsoft.UI.Dispatching;
using Windows.UI.ViewManagement;

namespace MiaDock.UI.Services;

public sealed class ThemeService : IThemeService, IDisposable
{
    private readonly UISettings _uiSettings = new();
    private readonly DispatcherQueue? _dispatcher = DispatcherQueue.GetForCurrentThread();
    private ResourceDictionary? _styleDictionary;
    private ResourceDictionary? _customDictionary;
    private AppearanceSettings? _lastAppearance;
    private int _environmentRefreshPending;
    private bool _disposed;

    public ThemeService() => _uiSettings.ColorValuesChanged += OnColorValuesChanged;

    public ThemeStyle CurrentStyle { get; private set; } = ThemeStyle.AppleLike;

    public ThemeDescriptor CurrentDescriptor => ThemeCatalog.Get(CurrentStyle);

    public IReadOnlyList<ThemeDescriptor> AvailableThemes => ThemeCatalog.All;

    public event EventHandler? ThemeEnvironmentChanged;

    public void Apply(ThemeStyle style)
    {
        var resources = Application.Current.Resources.MergedDictionaries;
        var descriptor = ThemeCatalog.Get(style);
        var styleChanged = _styleDictionary is null || CurrentStyle != style;

        if (styleChanged)
        {
            if (_customDictionary is not null)
            {
                resources.Remove(_customDictionary);
            }

            if (_styleDictionary is not null)
            {
                resources.Remove(_styleDictionary);
            }

            _styleDictionary = new ResourceDictionary
            {
                Source = new Uri($"ms-appx:///Themes/{descriptor.ResourceFileName}")
            };
            resources.Add(_styleDictionary);
        }

        if (_customDictionary is null)
        {
            _customDictionary = new ResourceDictionary();
            resources.Add(_customDictionary);
        }
        else if (styleChanged)
        {
            ClearCustomResources();
            // Custom values must remain after the theme dictionary so they
            // continue to override its tokens without rebuilding controls.
            resources.Add(_customDictionary);
        }

        CurrentStyle = style;
    }

    public void Apply(AppearanceSettings appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        _lastAppearance = appearance;
        Apply(appearance.Theme);

        if (appearance.Theme == ThemeStyle.AdaptiveFluent)
        {
            ApplyAdaptiveFluentPalette();
            return;
        }

        var surfaceColor = ParseColor(appearance.BackgroundColor);
        var accentColor = ParseColor(appearance.AccentColor);
        _customDictionary!["IslandStyleSurfaceBrush"] = CreateSurfaceBrush(
            appearance.Theme,
            surfaceColor,
            appearance.Opacity);

        if (appearance.Theme.UsesAdaptiveSolidPalette())
        {
            if (appearance.Theme == ThemeStyle.OledBlack)
            {
                surfaceColor = Color.FromArgb(255, 0, 0, 0);
                _customDictionary!["IslandStyleSurfaceBrush"] = new SolidColorBrush(surfaceColor);
            }
            ApplyAdaptiveSolidPalette(surfaceColor, accentColor);
            return;
        }

        _customDictionary!["IslandStyleControlBrush"] = new SolidColorBrush(Color.FromArgb(
            appearance.Theme.UsesColorlessGlass() ? (byte)0x20 : (byte)0x38,
            accentColor.R,
            accentColor.G,
            accentColor.B));
        _customDictionary["IslandStyleAccentBrush"] = new SolidColorBrush(accentColor);
    }

    private void ClearCustomResources()
    {
        _customDictionary!.Clear();
        // ThemeDictionaries is a separate collection; ResourceDictionary.Clear
        // does not remove it. Leaving these entries behind pins Apple/OLED/pink
        // colors over Adaptive Fluent's live system palette.
        _customDictionary.ThemeDictionaries.Clear();
    }

    private void ApplyAdaptiveFluentPalette()
    {
        var background = _uiSettings.GetColorValue(UIColorType.Background);
        var foreground = _uiSettings.GetColorValue(UIColorType.Foreground);
        var accent = _uiSettings.GetColorValue(UIColorType.Accent);
        var palette = SolidThemeContrastPaletteFactory.Create(background, accent);
        var secondary = palette.Secondary;
        var isLight = RelativeLuminance(background) >= 0.5;
        var surface = Color.FromArgb(isLight ? (byte)0xE6 : (byte)0xD9, background.R, background.G, background.B);
        var control = Color.FromArgb(isLight ? (byte)0x18 : (byte)0x28, foreground.R, foreground.G, foreground.B);

        var resources = _customDictionary!;
        resources["IslandSurfaceBrush"] = new SolidColorBrush(surface);
        resources["IslandStyleSurfaceBrush"] = new SolidColorBrush(surface);
        resources["IslandSurfaceSecondaryBrush"] = new SolidColorBrush(control);
        resources["IslandTextPrimaryBrush"] = new SolidColorBrush(foreground);
        resources["IslandTextSecondaryBrush"] = new SolidColorBrush(secondary);
        resources["IslandAccentBrush"] = new SolidColorBrush(accent);
        resources["IslandStrokeBrush"] = new SolidColorBrush(Color.FromArgb(
            isLight ? (byte)0x22 : (byte)0x32,
            foreground.R,
            foreground.G,
            foreground.B));
        resources["IslandControlFillBrush"] = new SolidColorBrush(control);
        resources["IslandStyleControlBrush"] = new SolidColorBrush(control);
        resources["IslandStyleAccentBrush"] = new SolidColorBrush(accent);
        resources["IslandAccentForegroundBrush"] = new SolidColorBrush(palette.AccentForeground);
        resources["IslandIconButtonRestBrush"] = new SolidColorBrush(Color.FromArgb(
            isLight ? (byte)0x0D : (byte)0x18,
            foreground.R,
            foreground.G,
            foreground.B));
        resources["IslandIconButtonPointerOverBrush"] = new SolidColorBrush(Color.FromArgb(
            isLight ? (byte)0x1F : (byte)0x32,
            foreground.R,
            foreground.G,
            foreground.B));
        resources["IslandIconButtonPressedBrush"] = new SolidColorBrush(Color.FromArgb(
            isLight ? (byte)0x33 : (byte)0x4A,
            foreground.R,
            foreground.G,
            foreground.B));
        resources["IslandIconButtonForegroundBrush"] = new SolidColorBrush(foreground);
    }

    private static double RelativeLuminance(Color color) =>
        (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / byte.MaxValue;

    private static Brush CreateSurfaceBrush(ThemeStyle style, Color color, double opacity)
    {
        var normalizedOpacity = Math.Clamp(opacity, 0.35, 1);
        return style switch
        {
            ThemeStyle.Windows11Mica => new SolidColorBrush(WithOpacity(color, 0.91 * normalizedOpacity)),
            ThemeStyle.Windows11MicaAlt => new SolidColorBrush(WithOpacity(color, 0.86 * normalizedOpacity)),
            ThemeStyle.Windows11Acrylic => CreateAcrylic(color, 0.72, normalizedOpacity),
            ThemeStyle.Windows11AcrylicThin => CreateAcrylic(color, 0.46, normalizedOpacity),
            ThemeStyle.BlurredGlass or ThemeStyle.NeutralFrostedGlass => CreateGlassOverlay(normalizedOpacity),
            ThemeStyle.OledBlack => new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
            ThemeStyle.TozPembe => new SolidColorBrush(Color.FromArgb(255, color.R, color.G, color.B)),
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
        WriteSolidPalette(_customDictionary!, surfaceColor, palette);
        foreach (var name in new[] { "Default", "Light" })
        {
            WriteSolidPalette(GetOrCreateThemeDictionary(_customDictionary!, name), surfaceColor, palette);
        }
    }

    private static ResourceDictionary GetOrCreateThemeDictionary(ResourceDictionary parent, string name)
    {
        if (parent.ThemeDictionaries.TryGetValue(name, out var existing) &&
            existing is ResourceDictionary dictionary)
        {
            return dictionary;
        }

        var created = new ResourceDictionary();
        parent.ThemeDictionaries[name] = created;
        return created;
    }

    private static void WriteSolidPalette(
        ResourceDictionary target,
        Color surfaceColor,
        SolidThemeContrastPalette palette)
    {
        target["IslandSurfaceBrush"] = new SolidColorBrush(surfaceColor);
        target["IslandStyleSurfaceBrush"] = new SolidColorBrush(surfaceColor);
        target["IslandSurfaceSecondaryBrush"] = new SolidColorBrush(palette.Control);
        target["IslandTextPrimaryBrush"] = new SolidColorBrush(palette.Primary);
        target["IslandTextSecondaryBrush"] = new SolidColorBrush(palette.Secondary);
        target["IslandAccentBrush"] = new SolidColorBrush(palette.Accent);
        target["IslandStrokeBrush"] = new SolidColorBrush(palette.Stroke);
        target["IslandControlFillBrush"] = new SolidColorBrush(Color.FromArgb(
            38,
            palette.Primary.R,
            palette.Primary.G,
            palette.Primary.B));
        target["IslandStyleControlBrush"] = new SolidColorBrush(palette.Control);
        target["IslandStyleAccentBrush"] = new SolidColorBrush(palette.Accent);
        target["IslandAccentForegroundBrush"] = new SolidColorBrush(palette.AccentForeground);
        target["IslandIconButtonRestBrush"] = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        target["IslandIconButtonPointerOverBrush"] = new SolidColorBrush(Color.FromArgb(
            40,
            palette.Primary.R,
            palette.Primary.G,
            palette.Primary.B));
        target["IslandIconButtonPressedBrush"] = new SolidColorBrush(Color.FromArgb(
            66,
            palette.Primary.R,
            palette.Primary.G,
            palette.Primary.B));
        target["IslandIconButtonForegroundBrush"] = new SolidColorBrush(palette.Primary);
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

    private void OnColorValuesChanged(UISettings sender, object args)
    {
        if (_disposed || CurrentStyle != ThemeStyle.AdaptiveFluent)
        {
            return;
        }

        if (Interlocked.Exchange(ref _environmentRefreshPending, 1) != 0)
        {
            return;
        }

        void Refresh()
        {
            try
            {
                if (_disposed || _lastAppearance is null || CurrentStyle != ThemeStyle.AdaptiveFluent)
                {
                    return;
                }

                Apply(_lastAppearance);
                ThemeEnvironmentChanged?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                Interlocked.Exchange(ref _environmentRefreshPending, 0);
            }
        }

        if (_dispatcher?.HasThreadAccess != false)
        {
            Refresh();
        }
        else
        {
            if (!_dispatcher.TryEnqueue(Refresh))
            {
                Interlocked.Exchange(ref _environmentRefreshPending, 0);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _uiSettings.ColorValuesChanged -= OnColorValuesChanged;
    }
}
