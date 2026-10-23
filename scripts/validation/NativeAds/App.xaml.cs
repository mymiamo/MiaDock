using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using MiaDock.App.Controls;
using MiaDock.App.Services;
using MiaDock.Core.Advertising;
using MiaDock.Core.Applications;
using MiaDock.Core.Settings;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace MiaDock.NativeAds;

public sealed partial class App : Application
{
    private readonly List<string> _checks = [];
    private Window? _window;
    private MiamoAdsSlider _slider = null!;
    private NavigationView _navigation = null!;
    private AdSliderSession Session => (AdSliderSession)typeof(MiamoAdsSlider).GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_slider)!;
    private static MiamoAd Ad(string id) => new(id, new($"https://mymiamo.net/ads/{id}.png"), new($"https://mymiamo.net/click/{id}"), "public-test-token");
    public App() => InitializeComponent();
    private void Check(bool condition, string description)
    { if (!condition) throw new InvalidOperationException(description); _checks.Add(description); }
    private async Task WaitFor(Func<bool> condition, string message, int milliseconds = 5000)
    {
        _window?.Activate();
        var until = DateTime.UtcNow.AddMilliseconds(milliseconds);
        var lastActivation = DateTime.UtcNow;
        while (!condition() && DateTime.UtcNow < until)
        {
            await Task.Delay(50);
            if (_window is not null && DateTime.UtcNow - lastActivation > TimeSpan.FromMilliseconds(500))
            { _window.Activate(); lastActivation = DateTime.UtcNow; }
        }
        Check(condition(), message);
    }
    private void ClickNext() => typeof(MiamoAdsSlider).GetMethod("OnNextClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_slider, [_slider, new RoutedEventArgs()]);
    private Task Refresh() => (Task)typeof(MiamoAdsSlider).GetMethod("RefreshAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_slider, [])!;
    private void Reconcile() => typeof(MiamoAdsSlider).GetMethod("Reconcile", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_slider, []);
    private void TogglePane()
    {
        Button? Find(DependencyObject root)
        {
            if (root is Button button && button.Name == "TogglePaneButton") return button;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
                if (Find(VisualTreeHelper.GetChild(root, i)) is { } match) return match;
            return null;
        }
        var button = Find(_navigation) ?? throw new InvalidOperationException("No pane toggle");
        var peer = FrameworkElementAutomationPeer.CreatePeerForElement(button) ?? new ButtonAutomationPeer(button);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var output = Environment.GetCommandLineArgs().Skip(1).First();
        var passed = false;
        string? failure = null;
        FakeClient? client = null;
        Window? cleanupGuard = null;
        try
        {
            _slider = new MiamoAdsSlider();
            _navigation = new NavigationView { OpenPaneLength = 264, IsPaneOpen = true, PaneDisplayMode = NavigationViewPaneDisplayMode.Left, IsSettingsVisible = false };
            client = new FakeClient(await CreateCreative());
            _window = new Window { Title = "MiaDock Miamo Ads — isolated native validation" };
            _navigation.RequestedTheme = ElementTheme.Dark;
            foreach (var title in new[] { "Overview", "Personalize", "Focus", "Modules", "Shortcuts", "System", "Support" })
                _navigation.MenuItems.Add(new NavigationViewItem { Content = title, Height = 40 });
            _navigation.FooterMenuItems.Add(new NavigationViewItem { Content = "What's New", Height = 40 });
            _navigation.PaneFooter = _slider;
            _navigation.Content = new TextBlock { Text = "Native production advertising component with a fake public API.", Margin = new Thickness(32) };
            _window.Content = _navigation;
            var launcher = new FakeLauncher();
            _slider.Configure(_window, _navigation, client, launcher, new Localization());
            _window.AppWindow.Resize(new SizeInt32(1040, 900));
            _window.Activate();
            await WaitFor(() => Session.IsVisible, "Decoded image becomes visibly presented");
            var imageButton = (Button)_slider.FindName("CreativeButton");
            var sequence = (Grid)_slider.FindName("SequenceControls");
            Check(imageButton.ActualWidth is >= 220 and <= 240, "Normal pane uses a 220–240 DIP square without changing its width");
            Check(Math.Abs(imageButton.ActualWidth - imageButton.ActualHeight) < 1, "Creative area remains square");
            Check(((Image)_slider.FindName("CreativeImage")).Stretch == Stretch.Uniform, "Image uses Uniform stretching");
            var footerItem = (FrameworkElement)_navigation.ContainerFromMenuItem(_navigation.FooterMenuItems[0]);
            var adTop = _slider.TransformToVisual(_navigation).TransformPoint(new Point(0, 0)).Y;
            var footerTop = footerItem.TransformToVisual(_navigation).TransformPoint(new Point(0, 0)).Y;
            Check(footerTop >= adTop + _slider.ActualHeight - 1, "Advertisement appears immediately above What's New");
            await Capture(output + ".png");
            typeof(MiamoAdsSlider).GetMethod("OnCreativeClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_slider, [_slider, new RoutedEventArgs()]);
            Check(launcher.LastUri == Session.Current!.ClickUrl, "Visible advertisement passes the trusted click URL to the system launcher abstraction");
            await WaitFor(() => client.Posts.Count == 1, "First visible campaign gets one impression");
            var first = Session.Current!.Key;
            Check(Session.VisibleTime < TimeSpan.FromSeconds(5), "Initial interaction occurs before five visible seconds");
            ClickNext(); Check(Session.Current!.Key == first, "Manual advance is rejected before minimum exposure");
            Check(!client.Posts.Any(p => p.CampaignId == "b"), "Preloaded next image has no impression");
            var before = Session.VisibleTime;
            TogglePane();
            await Task.Delay(6200);
            Check(_slider.Visibility == Visibility.Collapsed && Session.Current!.Key == first, "Collapsed pane hides the ad and does not advance it");
            typeof(MiamoAdsSlider).GetMethod("OnCreativeClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_slider, [_slider, new RoutedEventArgs()]);
            Check(launcher.Launches == 1, "Hidden advertisement cannot launch a click");
            Check((Session.VisibleTime - before).TotalSeconds < .3, "Collapsed time does not consume the remaining minimum");
            TogglePane();
            await WaitFor(() => Session.IsVisible, "Reopened pane resumes the same image");
            var presenter = (OverlappedPresenter)_window.AppWindow.Presenter;
            presenter.Minimize(); await Task.Delay(300); var minimizedAt = Session.VisibleTime;
            await Task.Delay(1500);
            Check((Session.VisibleTime - minimizedAt).TotalSeconds < .1, "Minimized window pauses exposure");
            presenter.Restore(); _window.Activate(); await WaitFor(() => Session.IsVisible, "Restored window resumes exposure");
            var other = new Window { Title = "Native activation probe", Content = new TextBlock { Text = "Activation pause" } };
            other.Activate(); await Task.Delay(300); var inactiveAt = Session.VisibleTime;
            await Task.Delay(1500);
            Check((Session.VisibleTime - inactiveAt).TotalSeconds < .1, "Inactive window pauses exposure");
            other.Close(); _window.Activate(); await WaitFor(() => Session.IsVisible, "Reactivation resumes exposure");
            imageButton.Focus(FocusState.Keyboard);
            await WaitFor(() => Session.CanMove, "Manual navigation becomes available after five visible seconds", 7000);
            ClickNext();
            await WaitFor(() => Session.Current?.CampaignId == "b" && Session.IsVisible, "Broken creative is skipped and next good creative is shown");
            await WaitFor(() => client.Posts.Count == 2, "Second visible campaign gets its own impression");
            Check(client.ListRequests == 1, "Slideshow and pane navigation do not refetch the campaign API");
            Check(client.Posts.Select(p => p.EventId).Distinct().Count() == 2, "Different impressions have distinct UUIDs");
            client.ThrowOnList = true; await Refresh();
            Check(Session.Current?.CampaignId == "b", "API outage retains the current good image");
            client.ThrowOnList = false;
            client.Ads = [Ad("a")]; await Refresh();
            await WaitFor(() => Session.Current?.CampaignId == "a" && Session.IsVisible, "Refreshed single campaign starts after the previous minimum", 7000);
            await Task.Delay(6500);
            Check(Session.Current?.CampaignId == "a" && sequence.Visibility == Visibility.Collapsed, "Single campaign stays static without slider controls");
            Check(client.Posts.Count(p => p.CampaignId == "a") == 1, "Returning to a campaign does not count another impression");
            _window.AppWindow.Resize(new SizeInt32(1040, 420)); await Task.Delay(500); Reconcile();
            Check(_slider.Visibility == Visibility.Collapsed, "Small window removes advertising space for navigation");
            _window.AppWindow.Resize(new SizeInt32(1040, 900)); await WaitFor(() => Session.IsVisible, "Growing the window restores the same advertisement");
            client.Ads = []; await Refresh();
            await WaitFor(() => Session.Current is null && _slider.Visibility == Visibility.Collapsed, "Empty API list removes advertisement and its margins");
            client.Ads = [Ad("broken")]; await Refresh(); await Task.Delay(600);
            Check(_slider.Visibility == Visibility.Collapsed, "All broken images leave no advertisement area");
            client.BlockList = true; var pending = Refresh(); await Task.Delay(50);
            cleanupGuard = new Window { Title = "Native close lifecycle probe", Content = new TextBlock { Text = "Checking advertising cleanup" } };
            cleanupGuard.Activate();
            var closingWindow = _window; _window = null; closingWindow.Close(); await pending;
            Check(client.CancellationObserved, "Closing the owning window cancels an outstanding request");
            Check(typeof(MiamoAdsSlider).GetField("_timer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_slider) is null, "Disposal releases the single timer");
            Check(!Session.IsVisible, "Disposed advertisements cannot accrue exposure");
            passed = true;
        }
        catch (Exception exception) { failure = exception.ToString(); }
        finally
        {
            object? Field(string name) => _slider?.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_slider);
            var details = new { ListRequests = client?.ListRequests, Side = Field("_side"), Active = Field("_active"),
                Scale = _navigation?.XamlRoot?.RasterizationScale,
                Loaded = Field("_loaded"), SourceReady = Field("_sourceReady"), Viewport = Field("_viewportVisible"), PaneOpen = _navigation?.IsPaneOpen,
                Frame = Field("_frameConfirmed"), Visible = _slider?.Visibility.ToString(),
                ImageLoaded = _slider is not null && Session.ImageLoaded, Campaign = _slider is null ? null : Session.Current?.CampaignId,
                GridWidth = (Field("_itemsGrid") as FrameworkElement)?.ActualWidth,
                GridHeight = (Field("_itemsGrid") as FrameworkElement)?.ActualHeight,
                MenuHeight = (Field("_menuHost") as FrameworkElement)?.DesiredSize.Height,
                Extent = (Field("_menuScroll") as ScrollViewer)?.ExtentHeight,
                FooterHeight = (Field("_footerHost") as FrameworkElement)?.DesiredSize.Height };
            _slider?.Dispose();
            File.WriteAllText(output, JsonSerializer.Serialize(new { Passed = passed, Checks = _checks, Failure = failure,
                Details = details,
                Impressions = client?.Posts.Select(p => new { p.CampaignId, p.EventId }).ToArray(),
                Scope = "Real WinUI component and window transitions with fake HTTP/image data. Not a live Miamo campaign test." }, new JsonSerializerOptions { WriteIndented = true }));
            Environment.ExitCode = passed ? 0 : 1; _window?.Close(); cleanupGuard?.Close(); Exit();
        }
    }

    private static async Task<byte[]> CreateCreative()
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        var pixels = new byte[500 * 500 * 4];
        for (var i = 0; i < pixels.Length; i += 4) { pixels[i] = 40; pixels[i + 1] = 140; pixels[i + 2] = 170; pixels[i + 3] = 255; }
        encoder.SetPixelData(BitmapPixelFormat.Rgba8, BitmapAlphaMode.Premultiplied, 500, 500, 96, 96, pixels);
        await encoder.FlushAsync(); stream.Seek(0);
        using var reader = new DataReader(stream.GetInputStreamAt(0)); await reader.LoadAsync((uint)stream.Size);
        var bytes = new byte[stream.Size]; reader.ReadBytes(bytes); return bytes;
    }
    private async Task Capture(string path)
    {
        var target = new RenderTargetBitmap(); await target.RenderAsync(_navigation);
        var pixels = await target.GetPixelsAsync();
        using var reader = DataReader.FromBuffer(pixels); var bytes = new byte[pixels.Length]; reader.ReadBytes(bytes);
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)target.PixelWidth, (uint)target.PixelHeight, 96, 96, bytes);
        await encoder.FlushAsync(); stream.Seek(0);
        using var encoded = new DataReader(stream.GetInputStreamAt(0)); await encoded.LoadAsync((uint)stream.Size);
        var png = new byte[stream.Size]; encoded.ReadBytes(png); await File.WriteAllBytesAsync(path, png);
    }
    private sealed class FakeClient(byte[] creative) : IMiamoAdsClient
    {
        public IReadOnlyList<MiamoAd> Ads = [Ad("a"), Ad("broken"), Ad("b")];
        public readonly List<MiamoImpression> Posts = [];
        public int ListRequests;
        public bool ThrowOnList, BlockList, CancellationObserved;
        public async Task<IReadOnlyList<MiamoAd>> GetAdsAsync(CancellationToken cancellationToken)
        {
            ListRequests++;
            if (BlockList)
            {
                try { await Task.Delay(Timeout.Infinite, cancellationToken); }
                catch (OperationCanceledException) { CancellationObserved = true; throw; }
            }
            if (ThrowOnList) throw new HttpRequestException("Fake outage");
            return Ads;
        }
        public Task<byte[]> GetImageAsync(Uri uri, CancellationToken cancellationToken) => uri.AbsolutePath.Contains("broken", StringComparison.Ordinal)
            ? Task.FromException<byte[]>(new InvalidDataException("Fake corrupt creative")) : Task.FromResult(creative);
        public Task<ImpressionResult> PostImpressionAsync(MiamoImpression impression, CancellationToken cancellationToken)
        { Posts.Add(impression); return Task.FromResult(new ImpressionResult(true, false)); }
    }
    private sealed class FakeLauncher : IExternalUriLauncher
    {
        public Uri? LastUri;
        public int Launches;
        public Task<bool> LaunchAsync(Uri uri, CancellationToken cancellationToken = default)
        { LastUri = uri; Launches++; return Task.FromResult(true); }
    }
    private sealed class Localization : IAppLocalizationService
    {
        public AppLanguage CurrentLanguage => (AppLanguage)1;
        public CultureInfo CurrentCulture => CultureInfo.GetCultureInfo("en-US");
        public event EventHandler? LanguageChanged { add { } remove { } }
        public void SetLanguage(AppLanguage language) { }
        public string Get(string key, params object?[] arguments) => key;
        public string Text(string turkish, string english) => english;
        public void Apply(DependencyObject root) { }
    }
}
