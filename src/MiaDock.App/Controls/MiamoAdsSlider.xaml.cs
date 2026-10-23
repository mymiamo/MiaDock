using System.Diagnostics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using MiaDock.App.Services;
using MiaDock.Core.Advertising;
using MiaDock.Core.Applications;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace MiaDock.App.Controls;

/// <summary>One component/session/timer per settings window; navigation pages do not own advertising.</summary>
public sealed partial class MiamoAdsSlider : UserControl, IDisposable
{
    private readonly AdSliderSession _session = new();
    private readonly AdImpressionLedger _impressions = new();
    private readonly AdRefreshSchedule _refreshSchedule = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<Uri, Task<BitmapImage>> _bitmaps = [];
    private Window? _owner;
    private FrameworkElement? _ownerRoot;
    private NavigationView? _navigation;
    private IMiamoAdsClient? _client;
    private IExternalUriLauncher? _launcher;
    private IAppLocalizationService? _localization;
    private DispatcherQueueTimer? _timer;
    private FrameworkElement? _itemsGrid, _menuHost, _footerHost;
    private CancellationTokenSource? _impressionCancellation;
    private long _paneCallback, _visibilityCallback;
    private long _revision = -1;
    private Uri? _preloading;
    private double _side;
    private bool _active, _loaded, _disposed, _sourceReady, _frameConfirmed, _rendering;
    private bool _viewportVisible, _keyboardFocus, _reconcilePending;
    private static TimeSpan Now => Stopwatch.GetElapsedTime(0);

    public MiamoAdsSlider()
    {
        InitializeComponent();
        Loaded += OnLoaded; Unloaded += OnUnloaded;
        EffectiveViewportChanged += OnEffectiveViewportChanged;
        GotFocus += OnGotFocus; LostFocus += OnLostFocus;
        _visibilityCallback = RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => RefreshVisibility());
    }

    public void Configure(Window owner, NavigationView navigation, IMiamoAdsClient client,
        IExternalUriLauncher launcher, IAppLocalizationService localization)
    {
        if (_owner is not null) throw new InvalidOperationException("Advertising is already attached to a window.");
        _owner = owner; _navigation = navigation; _client = client; _launcher = launcher; _localization = localization;
        _ownerRoot = owner.Content as FrameworkElement;
        if (_ownerRoot is not null) { _ownerRoot.Loaded += OnLoaded; _ownerRoot.Unloaded += OnOwnerUnloaded; }
        _timer = DispatcherQueue.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(250);
        _timer.Tick += OnTick;
        owner.Activated += OnActivated; owner.Closed += OnOwnerClosed;
        owner.AppWindow.Changed += OnWindowChanged;
        navigation.SizeChanged += OnNavigationSizeChanged;
        _paneCallback = navigation.RegisterPropertyChangedCallback(NavigationView.IsPaneOpenProperty, (_, _) => Reconcile());
        localization.LanguageChanged += OnLanguageChanged;
        Localize();
        if (_ownerRoot?.IsLoaded == true || IsLoaded) Start();
    }

    private void OnLoaded(object sender, RoutedEventArgs args) => Start();
    private void Start()
    {
        if (_disposed || _owner is null) return;
        _loaded = true;
        if (_timer?.IsRunning == false) _timer.Start();
        RefreshIfDue(); Reconcile();
    }
    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        // A collapsed/empty PaneFooter must still be able to fetch its first/next list.
        // The owning window root, rather than this optional element, owns the timer.
        _session.SetVisible(false, Now); _frameConfirmed = false; DetachRendering();
        _impressionCancellation?.Cancel();
    }
    private void OnOwnerUnloaded(object sender, RoutedEventArgs args)
    { _loaded = false; _timer?.Stop(); OnUnloaded(sender, args); }
    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        _active = args.WindowActivationState != WindowActivationState.Deactivated;
        Reconcile();
    }
    private void OnWindowChanged(AppWindow sender, AppWindowChangedEventArgs args) => Reconcile();
    private void OnNavigationSizeChanged(object sender, SizeChangedEventArgs args) => Reconcile();
    private void OnEffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
    {
        _viewportVisible = args.EffectiveViewport.Width >= ActualWidth - 1 && args.EffectiveViewport.Height >= ActualHeight - 1 &&
            args.EffectiveViewport.Width > 0 && args.EffectiveViewport.Height > 0;
        RefreshVisibility();
    }
    private void OnGotFocus(object sender, RoutedEventArgs args)
    {
        if (args.OriginalSource is Control { FocusState: FocusState.Keyboard }) _keyboardFocus = true;
    }
    private void OnLostFocus(object sender, RoutedEventArgs args) => DispatcherQueue.TryEnqueue(() =>
    {
        if (_disposed) return;
        var focused = FocusManager.GetFocusedElement(XamlRoot) as DependencyObject;
        _keyboardFocus = false;
        for (var current = focused; current is not null; current = VisualTreeHelper.GetParent(current))
            if (current == this) { _keyboardFocus = true; break; }
    });

    private bool PaneCanPresent => !_disposed && _loaded && _active && _navigation?.IsPaneOpen == true &&
        _owner?.AppWindow.IsVisible == true && XamlRoot?.IsHostVisible == true &&
        _owner.AppWindow.Presenter is not OverlappedPresenter { State: OverlappedPresenterState.Minimized };

    private void OnTick(DispatcherQueueTimer sender, object args)
    {
        if (_disposed) return;
        RefreshVisibility();
        _session.Tick(Now, autoAdvance: !_keyboardFocus);
        Reconcile(); RefreshIfDue();
        if (_impressionCancellation is null && _impressions.TryBegin(_session, Now) is { } impression)
            _ = PostImpressionAsync(impression);
    }

    private void Reconcile()
    {
        if (_disposed || _navigation is null) return;
        UpdateBudget();
        _session.SkipFailed(Now, PaneCanPresent && _side > 0);
        if (_revision != _session.Revision)
        {
            _revision = _session.Revision;
            _sourceReady = _frameConfirmed = false;
            CreativeImage.Source = null;
            if (_session.Current is { } ad) _ = PresentAsync(ad, _revision);
        }
        SequenceControls.Visibility = _session.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        SequenceLabel.Text = $"{_session.Position} / {_session.Count}";
        AutomationProperties.SetHelpText(CreativeButton, SponsoredLabel.Text + " · " + SequenceLabel.Text);
        PreviousButton.IsEnabled = NextButton.IsEnabled = _session.CanMove;
        Visibility = _sourceReady && _side > 0 && _navigation.IsPaneOpen ? Visibility.Visible : Visibility.Collapsed;
        RefreshVisibility();
        if (_sourceReady && PaneCanPresent && _session.Next is { } next && _preloading != next.CreativeUrl)
        {
            _preloading = next.CreativeUrl;
            _ = PreloadAsync(next.CreativeUrl);
        }
    }

    private void UpdateBudget()
    {
        _itemsGrid ??= Find<FrameworkElement>(_navigation!, "ItemsContainerGrid");
        _menuHost ??= Find<FrameworkElement>(_navigation!, "MenuItemsHost");
        _footerHost ??= Find<FrameworkElement>(_navigation!, "FooterMenuItemsHost");
        if (_itemsGrid is null || _menuHost is null || _footerHost is null) { _side = 0; return; }
        // ScrollViewer.ExtentHeight includes the stretched viewport's empty space.
        // Reserve the repeater's measured content height, so navigation gets priority.
        var availableHeight = _itemsGrid.ActualHeight - _menuHost.DesiredSize.Height - _footerHost.DesiredSize.Height - 8;
        var chrome = Math.Max(14, SponsoredLabel.ActualHeight) + 4 + 16 + (_session.Count > 1 ? 28 : 0);
        _side = AdPanelLayout.ImageSide(_itemsGrid?.ActualWidth ?? 0, availableHeight, chrome);
        if (_side > 0 && Math.Abs(CreativeButton.Width - _side) > .5 || _side > 0 && double.IsNaN(CreativeButton.Width))
            CreativeButton.Width = CreativeButton.Height = _side;
    }

    private void RefreshVisibility()
    {
        if (_disposed) return;
        var eligible = PaneCanPresent && IsLoaded && _side > 0 && Visibility == Visibility.Visible && _sourceReady && _viewportVisible;
        // Respect collapsed ancestors too; native window visibility alone is not enough.
        for (DependencyObject? current = this; eligible && current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is UIElement { Visibility: Visibility.Collapsed }) eligible = false;
        if (!eligible)
        {
            _session.SetVisible(false, Now); _frameConfirmed = false; DetachRendering();
            _impressionCancellation?.Cancel();
        }
        else if (_frameConfirmed) _session.SetVisible(true, Now);
        else if (!_rendering) { CompositionTarget.Rendering += OnRendering; _rendering = true; }
    }
    private void OnRendering(object? sender, object args)
    {
        if (!PaneCanPresent || !_viewportVisible || Visibility != Visibility.Visible || CreativeImage.ActualWidth <= 0)
        { DetachRendering(); return; }
        _frameConfirmed = true; DetachRendering(); _session.SetVisible(true, Now);
    }
    private void DetachRendering()
    {
        if (_rendering) CompositionTarget.Rendering -= OnRendering;
        _rendering = false;
    }

    private void RefreshIfDue()
    {
        if (_disposed || !_loaded || _client is null || !_refreshSchedule.TryBegin(Now)) return;
        _ = RefreshAsync();
    }
    private async Task RefreshAsync()
    {
        try
        {
            var ads = await _client!.GetAdsAsync(_lifetime.Token).ConfigureAwait(false);
            await OnUiAsync(() =>
            {
                _bitmaps.Clear(); _preloading = null;
                _session.QueueAds(ads, Now); Reconcile(); return Task.FromResult(true);
            }).ConfigureAwait(false);
        }
        catch (Exception) { /* Public endpoint outages retain the last list and never block navigation. */ }
        finally { DispatcherQueue.TryEnqueue(() => { if (!_disposed) _refreshSchedule.Complete(); }); }
    }

    private Task<BitmapImage> BitmapAsync(Uri uri)
    {
        if (_bitmaps.TryGetValue(uri, out var cached)) return cached;
        while (_bitmaps.Count >= 3)
        {
            var key = _bitmaps.Keys.FirstOrDefault(key => key != _session.Current?.CreativeUrl) ?? _bitmaps.Keys.First();
            _bitmaps.Remove(key);
        }
        return _bitmaps[uri] = DecodeAsync(uri);
    }
    private async Task<BitmapImage> DecodeAsync(Uri uri)
    {
        var bytes = await _client!.GetImageAsync(uri, _lifetime.Token).ConfigureAwait(false);
        return await OnUiAsync(async () =>
        {
            using var stream = new InMemoryRandomAccessStream();
            using var writer = new DataWriter(stream);
            writer.WriteBytes(bytes); await writer.StoreAsync(); stream.Seek(0);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            if (decoder.PixelWidth > 4096 || decoder.PixelHeight > 4096 || (long)decoder.PixelWidth * decoder.PixelHeight > 16 * 1024 * 1024)
                throw new InvalidDataException("Creative pixel dimensions exceed the limit.");
            stream.Seek(0);
            // One decode dimension preserves aspect ratio; never enlarge a small source.
            var bitmap = new BitmapImage { DecodePixelWidth = (int)Math.Min(500, decoder.PixelWidth), AutoPlay = false };
            await bitmap.SetSourceAsync(stream); _lifetime.Token.ThrowIfCancellationRequested();
            return bitmap;
        }).ConfigureAwait(false);
    }
    private async Task PresentAsync(MiamoAd ad, long revision)
    {
        try
        {
            var bitmap = await BitmapAsync(ad.CreativeUrl);
            if (_disposed || revision != _session.Revision) return;
            CreativeImage.Source = bitmap; _sourceReady = true;
            _session.MarkImageLoaded(ad.Key, Now); Reconcile();
        }
        catch (Exception)
        {
            if (_disposed || revision != _session.Revision) return;
            _session.MarkImageFailed(ad.CreativeUrl, Now); QueueReconcile();
        }
    }
    private async Task PreloadAsync(Uri uri)
    {
        try { await BitmapAsync(uri); }
        catch (Exception) { if (!_disposed) { _session.MarkImageFailed(uri, Now); QueueReconcile(); } }
    }
    private void OnImageFailed(object sender, ExceptionRoutedEventArgs args)
    {
        if (!_disposed && _session.Current is { } ad)
        { _session.MarkImageFailed(ad.CreativeUrl, Now); QueueReconcile(); }
    }

    private void QueueReconcile()
    {
        if (_disposed || _reconcilePending) return;
        _reconcilePending = true;
        DispatcherQueue.TryEnqueue(() => { _reconcilePending = false; Reconcile(); });
    }

    private async Task PostImpressionAsync(MiamoImpression impression)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _impressionCancellation = cancellation;
        var result = new ImpressionResult(false, true);
        try { result = await _client!.PostImpressionAsync(impression, cancellation.Token); }
        catch (Exception) { /* Retry with the same event_id only after becoming visible again. */ }
        finally
        {
            _impressionCancellation = null;
            if (!_disposed) _impressions.Complete(impression, result, Now);
        }
    }

    private async void OnCreativeClick(object sender, RoutedEventArgs args)
    {
        if (!_session.IsVisible || _session.Current is not { } ad || !MiamoAdPolicy.IsTrusted(ad.ClickUrl)) return;
        try { await _launcher!.LaunchAsync(ad.ClickUrl, _lifetime.Token); }
        catch (Exception) { /* Browser launch failure must not affect navigation. */ }
    }
    private void OnPreviousClick(object sender, RoutedEventArgs args) { _session.Move(-1, Now); Reconcile(); }
    private void OnNextClick(object sender, RoutedEventArgs args) { _session.Move(1, Now); Reconcile(); }
    private void OnLanguageChanged(object? sender, EventArgs args) => Localize();
    private void Localize()
    {
        if (_localization is null) return;
        SponsoredLabel.Text = _localization.Text("Sponsorlu", "Sponsored");
        AutomationProperties.SetName(CreativeButton, _localization.Text("Reklamı aç", "Open advertisement"));
        AutomationProperties.SetName(PreviousButton, _localization.Text("Önceki reklam", "Previous advertisement"));
        AutomationProperties.SetName(NextButton, _localization.Text("Sonraki reklam", "Next advertisement"));
        AutomationProperties.SetName(SequenceLabel, _localization.Text("Reklam sırası", "Advertisement position"));
    }

    private Task<T> OnUiAsync<T>(Func<Task<T>> callback)
    {
        if (_disposed) return Task.FromException<T>(new OperationCanceledException());
        if (DispatcherQueue.HasThreadAccess) return callback();
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!DispatcherQueue.TryEnqueue(async () =>
        {
            try { if (_disposed) throw new OperationCanceledException(); completion.TrySetResult(await callback()); }
            catch (Exception exception) { completion.TrySetException(exception); }
        })) completion.TrySetException(new OperationCanceledException());
        return completion.Task;
    }
    private static T? Find<T>(DependencyObject root, string name) where T : FrameworkElement
    {
        if (root is T element && element.Name == name) return element;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (Find<T>(VisualTreeHelper.GetChild(root, i), name) is { } match) return match;
        return null;
    }
    private void OnOwnerClosed(object sender, WindowEventArgs args) => Dispose();
    public void Dispose()
    {
        if (_disposed) return;
        _session.SetVisible(false, Now); _disposed = true;
        _lifetime.Cancel(); _impressionCancellation?.Cancel(); DetachRendering();
        if (_timer is not null) { _timer.Stop(); _timer.Tick -= OnTick; _timer = null; }
        if (_owner is not null)
        { _owner.Activated -= OnActivated; _owner.Closed -= OnOwnerClosed; _owner.AppWindow.Changed -= OnWindowChanged; }
        if (_ownerRoot is not null) { _ownerRoot.Loaded -= OnLoaded; _ownerRoot.Unloaded -= OnOwnerUnloaded; }
        if (_navigation is not null)
        { _navigation.SizeChanged -= OnNavigationSizeChanged; _navigation.UnregisterPropertyChangedCallback(NavigationView.IsPaneOpenProperty, _paneCallback); }
        if (_localization is not null) _localization.LanguageChanged -= OnLanguageChanged;
        UnregisterPropertyChangedCallback(VisibilityProperty, _visibilityCallback);
        Loaded -= OnLoaded; Unloaded -= OnUnloaded; EffectiveViewportChanged -= OnEffectiveViewportChanged;
        GotFocus -= OnGotFocus; LostFocus -= OnLostFocus;
        CreativeImage.Source = null; _bitmaps.Clear(); _lifetime.Dispose();
    }
}
