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
            _customDictionary.Clear();
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
        var surfaceColor = ParseColor(appearance.BackgroundColor);
        var accentColor = ParseColor(appearance.AccentColor);
        if (appearance.Theme != ThemeStyle.AdaptiveFluent)
        {
            _customDictionary!["IslandStyleSurfaceBrush"] = CreateSurfaceBrush(
                appearance.Theme,
                surfaceColor,
                appearance.Opacity);
        }

        if (appearance.Theme is ThemeStyle.AppleLike or ThemeStyle.CustomSolidColor or ThemeStyle.OledBlack)
        {
            if (appearance.Theme == ThemeStyle.OledBlack)
            {
                surfaceColor = Color.FromArgb(255, 0, 0, 0);
                _customDictionary!["IslandStyleSurfaceBrush"] = new SolidColorBrush(surfaceColor);
            }
            ApplyAdaptiveSolidPalette(surfaceColor, accentColor);
            return;
        }

        if (appearance.Theme != ThemeStyle.AdaptiveFluent)
        {
            _customDictionary!["IslandStyleControlBrush"] = new SolidColorBrush(Color.FromArgb(
                appearance.Theme.UsesColorlessGlass() ? (byte)0x20 : (byte)0x38,
                accentColor.R,
                accentColor.G,
                accentColor.B));
            _customDictionary["IslandStyleAccentBrush"] = new SolidColorBrush(accentColor);
        }
    }

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
        _customDictionary["IslandAccentForegroundBrush"] =
            new SolidColorBrush(palette.AccentForeground);
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
