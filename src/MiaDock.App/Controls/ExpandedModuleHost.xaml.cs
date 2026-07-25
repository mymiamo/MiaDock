using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Services;
using MiaDock.Core.Modules;

namespace MiaDock.App.Controls;

public sealed partial class ExpandedModuleHost : UserControl
{
    public static readonly DependencyProperty DisplayStateProperty = DependencyProperty.Register(
        nameof(DisplayState), typeof(ModuleDisplayState), typeof(ExpandedModuleHost),
        new PropertyMetadata(null, OnDisplayStateChanged));
    public static readonly DependencyProperty AvailableModulesProperty = DependencyProperty.Register(
        nameof(AvailableModules), typeof(IReadOnlyList<ModuleDisplayState>), typeof(ExpandedModuleHost),
        new PropertyMetadata(null, OnAvailableModulesChanged));

    private IModuleViewRegistry? _viewRegistry;
    private string? _activeViewKey;
    private bool _isHostActive;

    public ExpandedModuleHost() => InitializeComponent();

    public event EventHandler? PreviousRequested;
    public event EventHandler? NextRequested;
    public event EventHandler<ModuleSelectedEventArgs>? ModuleSelected;

    public ModuleDisplayState? DisplayState
    {
        get => (ModuleDisplayState?)GetValue(DisplayStateProperty);
        set => SetValue(DisplayStateProperty, value);
    }

    public IReadOnlyList<ModuleDisplayState>? AvailableModules
    {
        get => (IReadOnlyList<ModuleDisplayState>?)GetValue(AvailableModulesProperty);
        set => SetValue(AvailableModulesProperty, value);
    }

    public void Configure(IModuleViewRegistry viewRegistry)
    {
        _viewRegistry = viewRegistry ?? throw new ArgumentNullException(nameof(viewRegistry));
        Render();
    }

    public void SetHostActive(bool isActive)
    {
        _isHostActive = isActive;
        UpdateContentActivation();
    }

    private static void OnDisplayStateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var host = (ExpandedModuleHost)sender;
        host.Switcher.SelectedModuleId = host.DisplayState?.Descriptor.Id;
        host.Render();
    }

    private static void OnAvailableModulesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((ExpandedModuleHost)sender).Switcher.Modules = args.NewValue as IReadOnlyList<ModuleDisplayState>;

    private void Render()
    {
        if (DisplayState is not { } state)
        {
            DeactivateCurrentContent();
            ViewHost.Content = null;
            _activeViewKey = null;
            return;
        }

        var key = state.Descriptor.ExpandedViewKey;
        if (_activeViewKey != key || ViewHost.Content is null)
        {
            DeactivateCurrentContent();
            ViewHost.Content = _viewRegistry?.Create(key) ?? new GenericExpandedModuleView();
            _activeViewKey = key;
        }

        if (ViewHost.Content is GenericExpandedModuleView generic)
        {
            generic.DataContext = state.Presentation;
        }

        UpdateContentActivation();
    }

    private void OnPreviousRequested(object? sender, EventArgs args) => PreviousRequested?.Invoke(this, EventArgs.Empty);
    private void OnNextRequested(object? sender, EventArgs args) => NextRequested?.Invoke(this, EventArgs.Empty);

    private void OnModuleSelected(object? sender, ModuleSelectedEventArgs args) => ModuleSelected?.Invoke(this, args);

    private void UpdateContentActivation()
    {
        if (ViewHost.Content is IModuleViewActivationAware aware) aware.SetPresentationActive(_isHostActive);
    }

    private void DeactivateCurrentContent()
    {
        if (ViewHost.Content is IModuleViewActivationAware aware) aware.SetPresentationActive(false);
    }
}
