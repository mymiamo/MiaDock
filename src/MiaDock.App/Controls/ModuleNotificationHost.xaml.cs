using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Services;
using MiaDock.Core.Modules;
using MiaDock.Core.Localization;

namespace MiaDock.App.Controls;

public sealed partial class ModuleNotificationHost : UserControl
{
    public static readonly DependencyProperty DisplayStateProperty = DependencyProperty.Register(
        nameof(DisplayState), typeof(ModuleDisplayState), typeof(ModuleNotificationHost),
        new PropertyMetadata(null, OnDisplayStateChanged));
    public static readonly DependencyProperty ShowControlsProperty = DependencyProperty.Register(
        nameof(ShowControls), typeof(bool), typeof(ModuleNotificationHost),
        new PropertyMetadata(false, OnShowControlsChanged));

    private IModuleViewRegistry? _viewRegistry;
    private readonly Dictionary<string, FrameworkElement> _viewCache =
        new(StringComparer.Ordinal);
    private string? _activeViewKey;
    private ILocalizationService? _localization;

    public ModuleNotificationHost() => InitializeComponent();

    public event EventHandler<ModuleViewLoadFailedEventArgs>? ViewLoadFailed;

    public ModuleDisplayState? DisplayState
    {
        get => (ModuleDisplayState?)GetValue(DisplayStateProperty);
        set => SetValue(DisplayStateProperty, value);
    }

    public bool ShowControls
    {
        get => (bool)GetValue(ShowControlsProperty);
        set => SetValue(ShowControlsProperty, value);
    }

    public void Configure(IModuleViewRegistry viewRegistry)
    {
        _viewRegistry = viewRegistry ?? throw new ArgumentNullException(nameof(viewRegistry));
        Render();
    }

    public void ConfigureLocalization(ILocalizationService localization) =>
        _localization = localization;

    private static void OnDisplayStateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((ModuleNotificationHost)sender).Render();

    private static void OnShowControlsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((ModuleNotificationHost)sender).ApplyControlVisibility();

    private void Render()
    {
        if (DisplayState is not { } state)
        {
            ViewHost.Content = null;
            _activeViewKey = null;
            return;
        }

        var key = state.Descriptor.NotificationViewKey;
        if (_activeViewKey != key || ViewHost.Content is null)
        {
            ViewHost.Content = GetOrCreateView(
                key,
                static () => new GenericModuleNotificationView());
            _activeViewKey = key;
        }

        if (ViewHost.Content is GenericModuleNotificationView generic)
        {
            generic.DataContext = state.Presentation;
        }
        else if (ViewHost.Content is NotificationModuleNotificationView notification)
        {
            notification.DataContext = state.Presentation;
        }
        else if (ViewHost.Content is TransferNotificationView transfer)
        {
            transfer.DataContext = state.Presentation;
        }

        ApplyControlVisibility();
    }

    private void ApplyControlVisibility()
    {
        if (ViewHost.Content is TrackNotificationView musicView)
        {
            musicView.ShowControls = ShowControls;
        }
        else if (ViewHost.Content is VolumeNotificationView volumeView)
        {
            volumeView.ShowControls = ShowControls;
        }
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
                Padding = new Thickness(16, 10, 16, 10),
                Children =
                {
                    new TextBlock
                    {
                        Text = "MiaDock",
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                }
            };
        }

        _viewCache.Add(viewKey, view);
        return view;
    }
}
