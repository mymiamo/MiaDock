using System.Reflection;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using MiaDock.App.Controls;
using MiaDock.App.Services;
using MiaDock.Modules.Media.Models;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace MiaDock.NativeIndicators;

public sealed partial class App : Application
{
    private readonly List<string> _checks = [];
    private Window? _window;
    public App() => InitializeComponent();
    private void Check(bool condition, string text)
    { if (!condition) throw new InvalidOperationException(text); _checks.Add(text); }
    private static T Part<T>(Control control, string name) => (T)control.FindName(name);
    private static T? Field<T>(object control, string name) => (T?)control.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(control);

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var output = Environment.GetCommandLineArgs().Skip(1).First();
        var passed = false; string? failure = null; double scale = 0;
        try
        {
            var preferences = new Preferences();
            MiaDock.App.App.Services = preferences;
            var panel = new StackPanel { Spacing = 24, Padding = new Thickness(24), Background = new SolidColorBrush(Microsoft.UI.Colors.Black) };
            panel.Children.Add(new TextBlock { Text = "MiaDock — six centered audio bars / battery percentage", Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) });
            var meter = new AudioActivityIndicator { Width = 20, Height = 14, HorizontalAlignment = HorizontalAlignment.Left };
            panel.Children.Add(meter);
            var samples = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };
            foreach (var percent in new[] { 100d, 60, 20, 10, 0 })
            {
                var cell = new StackPanel { Spacing = 6 };
                cell.Children.Add(new BatteryStatusIcon { Width = 38, Height = 16, ShowPercentage = true, ChargePercent = percent, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) });
                cell.Children.Add(new TextBlock { Text = percent.ToString("0") + "%", Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), FontSize = 11 });
                samples.Children.Add(cell);
            }
            samples.Children.Add(new BatteryStatusIcon { Width = 38, Height = 16, ShowPercentage = true, ChargePercent = 85, IsCharging = true });
            panel.Children.Add(samples);
            var battery = new BatteryStatusIcon { Width = 38, Height = 16, ShowPercentage = true, ChargePercent = 100, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White) };
            panel.Children.Add(battery);
            _window = new Window { Title = "MiaDock owned indicator validation", Content = panel };
            _window.AppWindow.Resize(new SizeInt32(620, 340)); _window.Activate();
            await Task.Delay(500); scale = panel.XamlRoot.RasterizationScale;
            var names = new[] { "LeadingBar", "LeadingInnerBar", "CenterLeadingBar", "CenterBar", "TrailingInnerBar", "TrailingBar" };
            var bars = names.Select(name => Part<Rectangle>(meter, name)).ToArray();
            Check(bars.Length == 6 && bars.All(bar => bar.IsLoaded), "Six real bar elements are loaded");
            Check(bars.Sum(bar => bar.ActualWidth) + meter.BarSpacing * 5 <= meter.ActualWidth + .01, "Six bars fit the compact width without clipping");
            Check(bars.All(bar => Math.Abs(ElementCompositionPreview.GetElementVisual(bar).CenterPoint.Y - bar.ActualHeight / 2) < .01), "Every bar grows around the fixed vertical center");
            var resting = bars.Select(bar => ElementCompositionPreview.GetElementVisual(bar).Scale.Y).ToArray();
            Check(resting.Distinct().Count() == 1 && bars.Select((bar, index) => Math.Abs(resting[index] * bar.ActualHeight - bar.ActualWidth) < .01).All(value => value), "Paused state keeps six equal circular dots");
            meter.IsPlaying = true; meter.AudioLevelSource = MediaAudioLevelSource.SystemSpectrum;
            meter.TrebleLevel = .9; meter.BassLevel = .25; meter.VocalLevel = .6;
            preferences.Enabled = false; // Deterministic scale assertions with reduced motion.
            var levels = bars.Select(bar => ElementCompositionPreview.GetElementVisual(bar).Scale.Y).ToArray();
            Check(Math.Abs(levels[0] - .9) < .01 && Math.Abs(levels[2] - .25) < .01 && Math.Abs(levels[4] - .6) < .01, "Six-bar rendering follows the existing real spectrum sources");
            Check(levels[0] == levels[1] && levels[2] == levels[3] && levels[4] == levels[5], "Each pair belongs to one band when motion is reduced");
            meter.TrebleLevel = .3;
            var trebleChanged = bars.Select(bar => ElementCompositionPreview.GetElementVisual(bar).Scale.Y).ToArray();
            Check(Math.Abs(trebleChanged[0] - .3) < .01 && trebleChanged[0] == trebleChanged[1] && trebleChanged.Skip(2).SequenceEqual(levels.Skip(2)), "Treble changes only its own two bars");
            meter.BassLevel = .8;
            var bassChanged = bars.Select(bar => ElementCompositionPreview.GetElementVisual(bar).Scale.Y).ToArray();
            Check(Math.Abs(bassChanged[2] - .8) < .01 && bassChanged[2] == bassChanged[3] && bassChanged[0] == trebleChanged[0] && bassChanged[4] == trebleChanged[4], "Bass changes only its own two bars");
            preferences.Enabled = true; meter.AudioLevelSource = MediaAudioLevelSource.Unavailable;
            await Task.Delay(700);
            Check(Field<Microsoft.UI.Dispatching.DispatcherQueueTimer>(meter, "_decorativeTimer")?.IsRunning == true, "Fallback motion starts only while playing");
            meter.IsPlaying = false;
            var paused = bars.Select(bar => ElementCompositionPreview.GetElementVisual(bar).Scale.Y).ToArray();
            await Task.Delay(200);
            Check(paused.SequenceEqual(bars.Select(bar => ElementCompositionPreview.GetElementVisual(bar).Scale.Y)), "Paused bars do not move");
            Check(Field<Microsoft.UI.Dispatching.DispatcherQueueTimer>(meter, "_decorativeTimer")?.IsRunning != true, "Pause stops the decorative timer");
            meter.IsSuppressed = true;
            Check(paused.SequenceEqual(bars.Select(bar => ElementCompositionPreview.GetElementVisual(bar).Scale.Y)), "Suppression keeps the same resting dots");

            var fill = Part<Border>(battery, "ChargeFill");
            Check(Math.Abs(fill.Width - 22) < .01 && Part<TextBlock>(battery, "PercentageText").Text == "100", "Full charge fills the icon and overlays 100 inside it");
            Check(Part<Grid>(battery, "PercentageOverlay").Visibility == Visibility.Visible, "Percentage overlay is visible in main-dock mode");
            battery.ChargePercent = 50;
            Check(Math.Abs(fill.Width - 11) < .01, "Half charge produces half the fill");
            battery.ChargePercent = 0;
            Check(fill.Width == 0 && Part<TextBlock>(battery, "PercentageText").Text == "0", "Empty battery keeps a readable zero");
            var highContrast = new Windows.UI.ViewManagement.AccessibilitySettings().HighContrast;
            if (!highContrast)
            {
                battery.ChargePercent = 20;
                Check(((SolidColorBrush)fill.Background).Color.G == 199, "Low battery uses amber");
                battery.ChargePercent = 10;
                Check(((SolidColorBrush)fill.Background).Color.G == 69, "Critical battery uses red");
                battery.IsCharging = true;
                Check(((SolidColorBrush)fill.Background).Color.G == 195, "Charging takes precedence and uses green");
            }
            battery.IsCharging = true;
            Check(Part<Polygon>(battery, "ChargingBolt").Visibility == Visibility.Visible, "Charging bolt remains visible beside the percentage");
            battery.IsCharging = false; battery.ChargePercent = 150;
            Check(fill.Width == 22 && Part<TextBlock>(battery, "PercentageText").Text == "100", "Oversized readings clamp to 100");
            battery.ChargePercent = double.NaN;
            Check(fill.Width == 0 && Part<TextBlock>(battery, "PercentageText").Text == "?", "Invalid readings show unknown, without a false full charge");
            battery.IsBatteryPresent = false;
            Check(Part<TextBlock>(battery, "PercentageText").Text == "–", "Missing battery has an explicit unavailable state");
            battery.IsBatteryPresent = true; battery.ChargePercent = 100;
            await Capture(panel, output + ".png");
            panel.Children.Remove(meter); await Task.Delay(50);
            Check(Field<Microsoft.UI.Dispatching.DispatcherQueueTimer>(meter, "_decorativeTimer") is null, "Unload releases the timer");
            passed = true;
        }
        catch (Exception exception) { failure = exception.ToString(); }
        finally
        {
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { Passed = passed, Checks = _checks, Scale = scale, Failure = failure }, new JsonSerializerOptions { WriteIndented = true }));
            _window?.Close(); Exit();
        }
    }
    private static async Task Capture(FrameworkElement root, string path)
    {
        var bitmap = new RenderTargetBitmap(); await bitmap.RenderAsync(root);
        var pixels = await bitmap.GetPixelsAsync(); using var reader = DataReader.FromBuffer(pixels);
        var bytes = new byte[pixels.Length]; reader.ReadBytes(bytes);
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96, 96, bytes);
        await encoder.FlushAsync(); stream.Seek(0);
        using var encoded = new DataReader(stream.GetInputStreamAt(0)); await encoded.LoadAsync((uint)stream.Size);
        var png = new byte[stream.Size]; encoded.ReadBytes(png); await File.WriteAllBytesAsync(path, png);
    }
    private sealed class Preferences : IAnimationPreferenceService, IServiceProvider
    {
        private bool _enabled = true;
        public bool Enabled { get => _enabled; set { _enabled = value; AnimationsEnabledChanged?.Invoke(this, EventArgs.Empty); } }
        public bool AnimationsEnabled => _enabled;
        public event EventHandler? AnimationsEnabledChanged;
        public object? GetService(Type type) => type == typeof(IAnimationPreferenceService) ? this : null;
        public void Dispose() { }
    }
}
