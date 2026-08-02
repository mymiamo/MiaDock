using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Services;
using MiaDock.Core.Modules;
using MiaDock.Core.Localization;

namespace MiaDock.App.Controls;

public sealed partial class CompactModuleHost : UserControl
{
    public static readonly DependencyProperty DisplayStateProperty = DependencyProperty.Register(
        nameof(DisplayState), typeof(ModuleDisplayState), typeof(CompactModuleHost),
        new PropertyMetadata(null, OnDisplayStateChanged));

    private IModuleViewRegistry? _viewRegistry;
    private readonly Dictionary<string, FrameworkElement> _viewCache =
        new(StringComparer.Ordinal);
    private string? _activeViewKey;
    private ILocalizationService? _localization;
    private bool _isHostRequestedActive;
    private bool _isContentActive;
    private const string IdleCompactViewKey = "IdleCompactView";
    private const string IdleHoverViewKey = "IdleHoverView";

    public bool UseHoverView { get; set; }

    public CompactModuleHost() => InitializeComponent();

    public event EventHandler<ModuleViewLoadFailedEventArgs>? ViewLoadFailed;

    public ModuleDisplayState? DisplayState
    {
        get => (ModuleDisplayState?)GetValue(DisplayStateProperty);
        set => SetValue(DisplayStateProperty, value);
    }

    public void Configure(IModuleViewRegistry viewRegistry)
    {
        _viewRegistry = viewRegistry ?? throw new ArgumentNullException(nameof(viewRegistry));
        Render();
    }

    public void ConfigureLocalization(ILocalizationService localization)
    {
        _localization = localization;
        foreach (var generic in _viewCache.Values.OfType<GenericCompactModuleView>())
        {
            generic.ConfigureLocalization(localization);
        }
    }

    public void SetHostActive(bool isActive)
    {
        _isHostRequestedActive = isActive;
        UpdateContentActivation();
    }

    private static void OnDisplayStateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((CompactModuleHost)sender).Render();

    private void Render()
    {
        if (DisplayState is not { } state)
        {
            var idleViewKey = UseHoverView ? IdleHoverViewKey : IdleCompactViewKey;
            if (_activeViewKey != idleViewKey || ViewHost.Content is null)
            {
                DeactivateCurrentContent();
                ViewHost.Content = GetOrCreateView(idleViewKey, static () => new IdleCompactView());
                _activeViewKey = idleViewKey;
            }

            UpdateContentActivation();
            return;
        }

        var key = UseHoverView ? state.Descriptor.HoverViewKey : state.Descriptor.CompactViewKey;
        if (_activeViewKey != key || ViewHost.Content is null)
        {
            DeactivateCurrentContent();
            ViewHost.Content = GetOrCreateView(
                key,
                () => new GenericCompactModuleView(_localization));
            _activeViewKey = key;
        }

        if (ViewHost.Content is GenericCompactModuleView generic)
        {
            generic.DataContext = state.Presentation;
        }

        UpdateContentActivation();
    }

    private FrameworkElement GetOrCreateView(
        string viewKey,
        Func<FrameworkElement> fallbackFactory)
    {
        if (_viewCache.TryGetValue(viewKey, out var cached))
        {
            return cached;
        }

        FrameworkElement view;
        try
        {
            view = _viewRegistry?.Create(viewKey) ?? fallbackFactory();
        }
        catch (Exception exception)
        {
            ViewLoadFailed?.Invoke(
                this,
                new ModuleViewLoadFailedEventArgs(viewKey, exception));
            view = new Grid
            {
                Padding = new Thickness(12, 0, 12, 0),
                Children =
                {
                    new FontIcon
                    {
                        Glyph = "\uE783",
                        FontSize = 14,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            };
        }

        _viewCache.Add(viewKey, view);
        return view;
    }

    private void UpdateContentActivation()
    {
        if (ViewHost.Content is not IModuleViewActivationAware aware)
        {
            _isContentActive = false;
            return;
        }

        var shouldBeActive = _isHostRequestedActive && IsLoaded;
        if (_isContentActive == shouldBeActive)
        {
            return;
        }

        aware.SetPresentationActive(shouldBeActive);
        _isContentActive = shouldBeActive;
    }

    private void DeactivateCurrentContent()
    {
        if (ViewHost.Content is IModuleViewActivationAware aware)
        {
            aware.SetPresentationActive(false);
        }

        _isContentActive = false;
    }

    private void OnLoaded(object sender, RoutedEventArgs args) =>
        UpdateContentActivation();

    private void OnUnloaded(object sender, RoutedEventArgs args) =>
        DeactivateCurrentContent();
}
