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
    private string? _activeViewKey;
    private ILocalizationService? _localization;
    private const string IdleCompactViewKey = "IdleCompactView";
    private const string IdleHoverViewKey = "IdleHoverView";

    public bool UseHoverView { get; set; }

    public CompactModuleHost() => InitializeComponent();

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
        if (ViewHost.Content is GenericCompactModuleView generic)
        {
            generic.ConfigureLocalization(localization);
        }
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
                ViewHost.Content = _viewRegistry?.Create(idleViewKey) ?? new IdleCompactView();
                _activeViewKey = idleViewKey;
            }

            return;
        }

        var key = UseHoverView ? state.Descriptor.HoverViewKey : state.Descriptor.CompactViewKey;
        if (_activeViewKey != key || ViewHost.Content is null)
        {
            ViewHost.Content = _viewRegistry?.Create(key) ?? new GenericCompactModuleView(_localization);
            _activeViewKey = key;
        }

        if (ViewHost.Content is GenericCompactModuleView generic)
        {
            generic.DataContext = state.Presentation;
        }
    }
}
