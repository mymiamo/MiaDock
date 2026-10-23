using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.UI;
using Colors = Microsoft.UI.Colors;

namespace MiaDock.UI.Icons;

/// <summary>A local, theme-colored Ionicon. Glyph remains compatible with module bindings.</summary>
public sealed class DockIcon : ImageIcon
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(DockIcon), new PropertyMetadata(string.Empty, OnIconChanged));
    public static readonly DependencyProperty IconKeyProperty = DependencyProperty.Register(
        nameof(IconKey), typeof(string), typeof(DockIcon), new PropertyMetadata(string.Empty, OnIconChanged));
    public static readonly DependencyProperty IsFilledProperty = DependencyProperty.Register(
        nameof(IsFilled), typeof(bool), typeof(DockIcon), new PropertyMetadata(false, OnIconChanged));
    public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register(
        nameof(FontSize), typeof(double), typeof(DockIcon), new PropertyMetadata(20d, OnSizeChanged));

    private bool _loaded;
    private int _generation;
    private SolidColorBrush? _observedBrush;
    private long _colorToken;
    private long _opacityToken;
    private XamlRoot? _root;

    public DockIcon()
    {
        // FontIcon paints at FontSize even in a larger layout slot. ImageIcon
        // stretches its bitmap instead, so keep the semantic icon centered at
        // its measured size inside profile circles, hero tiles and grid cells.
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        UseLayoutRounding = true;
        IsHitTestVisible = false;
        AutomationProperties.SetAccessibilityView(this, AccessibilityView.Raw);
        RegisterPropertyChangedCallback(ForegroundProperty, (_, _) => { ObserveBrush(); Refresh(); });
        Loaded += (_, _) =>
        {
            _loaded = true;
            _root = XamlRoot;
            if (_root is not null) _root.Changed += OnRootChanged;
            DockIconPalette.Changed += OnPaletteChanged;
            ObserveBrush(); Refresh();
        };
        Unloaded += (_, _) =>
        {
            _loaded = false; _generation++; DetachBrush();
            if (_root is not null) _root.Changed -= OnRootChanged;
            _root = null;
            DockIconPalette.Changed -= OnPaletteChanged;
        };
        ActualThemeChanged += (_, _) => Refresh();
        SizeChanged += (_, _) => Refresh();
    }

    public string Glyph { get => (string)GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }
    public string IconKey { get => (string)GetValue(IconKeyProperty); set => SetValue(IconKeyProperty, value); }
    public bool IsFilled { get => (bool)GetValue(IsFilledProperty); set => SetValue(IsFilledProperty, value); }
    public double FontSize { get => (double)GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = double.IsFinite(FontSize) && FontSize > 0 ? FontSize : 20;
        var constrained = new Size(Math.Min(size, availableSize.Width), Math.Min(size, availableSize.Height));
        base.MeasureOverride(constrained);
        return constrained;
    }

    private static void OnIconChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((DockIcon)sender).Refresh();
    private static void OnSizeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var icon = (DockIcon)sender;
        icon.InvalidateMeasure(); icon.Refresh();
    }
    private void OnRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) => Refresh();
    private void OnPaletteChanged(object? sender, EventArgs args) => Refresh();

    private void ObserveBrush()
    {
        var brush = Foreground as SolidColorBrush;
        if (!_loaded || ReferenceEquals(_observedBrush, brush)) return;
        DetachBrush();
        _observedBrush = brush;
        if (brush is null) return;
        _colorToken = brush.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, (_, _) => Refresh());
        _opacityToken = brush.RegisterPropertyChangedCallback(Brush.OpacityProperty, (_, _) => Refresh());
    }

    private void DetachBrush()
    {
        if (_observedBrush is null) return;
        _observedBrush.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, _colorToken);
        _observedBrush.UnregisterPropertyChangedCallback(Brush.OpacityProperty, _opacityToken);
        _observedBrush = null;
    }

    private async void Refresh()
    {
        if (!_loaded) return;
        var generation = ++_generation;
        var brush = Foreground as SolidColorBrush;
        var color = brush?.Color ?? (ActualTheme == ElementTheme.Light ? Colors.Black : Colors.White);
        var alpha = (byte)Math.Round(color.A * (brush?.Opacity ?? 1));
        var argb = ((uint)alpha << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;
        argb = DockIconPalette.Resolve(argb);
        var size = double.IsFinite(FontSize) && FontSize > 0 ? FontSize : 20;
        if (ActualWidth > 0 && ActualHeight > 0)
        {
            // Explicit dimensions/alignment can still request a larger icon.
            // Decode for the final square inside that slot, not the old font size.
            size = Math.Min(ActualWidth, ActualHeight);
        }
        else
        {
            if (double.IsFinite(Width)) size = Math.Max(size, Width);
            if (double.IsFinite(Height)) size = Math.Max(size, Height);
        }
        var rasterSize = (uint)Math.Clamp(Math.Ceiling(size * (XamlRoot?.RasterizationScale ?? 1)), 1, 256);
        var asset = DockIconCatalog.AssetName(DockIconCatalog.Resolve(Glyph, IconKey), IsFilled);
        try
        {
            var image = await DockIconSourceCache.GetAsync(asset, argb, rasterSize);
            // A rapid module/theme change or unload must not install an old source.
            if (_loaded && generation == _generation) Source = image;
        }
        catch (Exception)
        {
            // Failed native decoding must not escape an async event handler.
            // Asset validation covers every source; a missing/failed icon stays empty.
            if (_loaded && generation == _generation) Source = null;
        }
    }
}

internal static class DockIconSourceCache
{
    private const int Capacity = 128;
    private static readonly Dictionary<(string Asset, uint Color, uint Size), Task<SvgImageSource>> Sources = [];
    private static readonly LinkedList<(string Asset, uint Color, uint Size)> Order = [];

    // All callers are XAML controls on the application dispatcher thread.
    public static Task<SvgImageSource> GetAsync(string asset, uint color, uint rasterSize)
    {
        var key = (asset, color, rasterSize);
        if (Sources.TryGetValue(key, out var existing))
        {
            Order.Remove(key); Order.AddLast(key);
            return existing;
        }
        var task = LoadAsync(asset, color, rasterSize);
        Sources.Add(key, task); Order.AddLast(key);
        while (Sources.Count > Capacity)
        {
            Sources.Remove(Order.First!.Value); Order.RemoveFirst();
        }
        return task;
    }

    private static async Task<SvgImageSource> LoadAsync(string asset, uint color, uint rasterSize)
    {
        var svg = DockIconSvg.Tint(DockIconSvg.ReadAsset(asset), color);
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteString(svg);
            await writer.StoreAsync();
            writer.DetachStream();
        }
        stream.Seek(0);
        var source = new SvgImageSource { RasterizePixelWidth = rasterSize, RasterizePixelHeight = rasterSize };
        var result = await source.SetSourceAsync(stream);
        if (result != SvgImageSourceLoadStatus.Success) throw new InvalidOperationException($"Dock SVG decode failed: {asset}, {result}");
        return source;
    }
}
