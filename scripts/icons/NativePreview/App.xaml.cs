using MiaDock.UI.Icons;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Text.Json;

namespace MiaDock.IconPreview;

/// <summary>Manual native rendering check, separate from the user's MiaDock instance and settings.</summary>
public sealed partial class App : Application
{
    private Window? _window;
    private readonly List<DockIcon> _icons = [];
    private readonly List<SolidColorBrush> _brushes = [];
    private readonly List<(string Name, DockIcon Icon, double Size)> _layoutCases = [];
    private readonly TextBlock _result = new() { FontSize = 16 };
    private readonly StackPanel _page = new() { Padding = new Thickness(20), Spacing = 12 };
    private readonly SolidColorBrush _background = new(Colors.Black);
    private bool _light;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var arguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var checkOnly = arguments.Contains("--check", StringComparer.Ordinal);
        var output = arguments.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal));
        _window = new Window { Title = "MiaDock Ionicons native preview" };
        var change = new Button { Content = "Switch dark / light and mutate theme brushes" };
        change.Click += (_, _) =>
        {
            _light = !_light;
            _background.Color = _light ? Colors.White : Colors.Black;
            _page.RequestedTheme = _light ? ElementTheme.Light : ElementTheme.Dark;
            foreach (var brush in _brushes) brush.Color = _light ? Colors.Black : Colors.White;
        };
        _page.Children.Add(change);
        _page.Children.Add(_result);
        _page.RequestedTheme = ElementTheme.Dark;
        _page.Children.Add(new TextBlock { Text = "Local outline / filled pairs; native raster sizes follow the current window DPI.", FontSize = 14 });
        var layoutRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        foreach (var (name, key, fontSize, boxSize) in new[]
        {
            ("Focus", "briefcase", 17d, 48d), ("Network", "wifi", 22d, 56d),
            ("Throughput", "arrow-down-circle", 16d, 32d), ("Power", "power", 22d, 44d),
            ("Privacy", "lock-closed", 24d, 56d), ("Call", "call", 28d, 72d)
        })
        {
            var brush = new SolidColorBrush(Colors.White);
            _brushes.Add(brush);
            var icon = new DockIcon { IconKey = key, FontSize = fontSize, Foreground = brush };
            _layoutCases.Add((name, icon, fontSize));
            var cell = new StackPanel { Spacing = 4 };
            cell.Children.Add(new TextBlock { Text = name, FontSize = 12 });
            cell.Children.Add(new Border
            {
                Width = boxSize, Height = boxSize, BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Colors.Gray),
                Child = new Grid { Children = { icon } }
            });
            layoutRow.Children.Add(cell);
        }
        // Explicit stretching is supported too, but must decode at the arranged size.
        var stretched = new DockIcon
        {
            IconKey = "wifi", FontSize = 16, HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch, Foreground = new SolidColorBrush(Colors.White)
        };
        _layoutCases.Add(("ExplicitStretch", stretched, 56));
        layoutRow.Children.Add(new Grid { Width = 56, Height = 56, Children = { stretched } });
        _page.Children.Add(layoutRow);
        var grid = new Grid { RowSpacing = 8, ColumnSpacing = 18 };
        for (var column = 0; column < 5; column++) grid.ColumnDefinitions.Add(new ColumnDefinition());
        var index = 0;
        foreach (var key in DockIconCatalog.Keys.Order(StringComparer.Ordinal))
        {
            if (index % 5 == 0) grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var entry = new StackPanel { Spacing = 4 };
            entry.Children.Add(new TextBlock { Text = key, FontSize = 12 });
            var pair = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            var brush = new SolidColorBrush(Colors.White);
            _brushes.Add(brush);
            foreach (var filled in new[] { false, true })
            {
                var icon = new DockIcon { IconKey = key, FontSize = 24, Foreground = brush, IsFilled = filled };
                _icons.Add(icon);
                pair.Children.Add(icon);
            }
            entry.Children.Add(pair);
            Grid.SetColumn(entry, index % 5); Grid.SetRow(entry, index / 5);
            grid.Children.Add(entry);
            index++;
        }
        _page.Children.Add(grid);
        _window.Content = new ScrollViewer { Content = _page, Background = _background };
        _window.AppWindow.Resize(new Windows.Graphics.SizeInt32(1120, 880));
        _page.Loaded += (_, _) =>
        {
            var timer = _page.DispatcherQueue.CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(3);
            timer.IsRepeating = false;
            timer.Tick += (_, _) =>
            {
                var missing = _icons.Where(icon => icon.Source is null).Select(icon => new { icon.IconKey, icon.IsFilled }).ToArray();
                var layoutCases = _layoutCases.Select(item => new
                {
                    item.Name, ExpectedSize = item.Size, item.Icon.ActualWidth, item.Icon.ActualHeight,
                    RasterSize = (item.Icon.Source as SvgImageSource)?.RasterizePixelWidth,
                    Passed = Math.Abs(item.Icon.ActualWidth - item.Size) < 0.01 &&
                        Math.Abs(item.Icon.ActualHeight - item.Size) < 0.01 &&
                        item.Icon.Source is SvgImageSource source &&
                        source.RasterizePixelWidth >= Math.Ceiling(item.Size * _page.XamlRoot.RasterizationScale)
                }).ToArray();
                var passed = missing.Length == 0 && layoutCases.All(item => item.Passed);
                _result.Text = $"Native sources loaded: {_icons.Count - missing.Length}/{_icons.Count}; window DPI scale: {_page.XamlRoot.RasterizationScale}";
                _result.Text += $"; layout checks: {layoutCases.Count(item => item.Passed)}/{layoutCases.Length}";
                _window.Title = $"MiaDock Ionicons native preview: {_icons.Count - missing.Length}/{_icons.Count}";
                if (output is not null) File.WriteAllText(output, JsonSerializer.Serialize(new
                {
                    Passed = passed, Total = _icons.Count, Loaded = _icons.Count - missing.Length, Missing = missing,
                    LayoutCases = layoutCases,
                    Scale = _page.XamlRoot.RasterizationScale,
                    Sizes = _icons.Select(icon => new { icon.IconKey, icon.IsFilled, icon.ActualWidth, icon.ActualHeight }).ToArray()
                }, new JsonSerializerOptions { WriteIndented = true }));
                if (checkOnly)
                {
                    Environment.ExitCode = passed ? 0 : 1;
                    Exit();
                }
            };
            timer.Start();
        };
        _window.Activate();
    }
}
