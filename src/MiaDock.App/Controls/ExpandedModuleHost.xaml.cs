using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Services;
using MiaDock.Core.Modules;
using MiaDock.Core.Localization;
using MiaDock.Core.Presentation;

namespace MiaDock.App.Controls;

public sealed partial class ExpandedModuleHost : UserControl
{
    private const string IdleExpandedViewKey = "IdleExpandedView";

    public static readonly DependencyProperty DisplayStateProperty = DependencyProperty.Register(
        nameof(DisplayState), typeof(ModuleDisplayState), typeof(ExpandedModuleHost),
        new PropertyMetadata(null, OnDisplayStateChanged));
    public static readonly DependencyProperty AvailableModulesProperty = DependencyProperty.Register(
        nameof(AvailableModules), typeof(IReadOnlyList<ModuleDisplayState>), typeof(ExpandedModuleHost),
        new PropertyMetadata(null, OnAvailableModulesChanged));

    private IModuleViewRegistry? _viewRegistry;
    private readonly Dictionary<string, FrameworkElement> _viewCache =
        new(StringComparer.Ordinal);
    private string? _activeViewKey;
    private string? _activeModuleId;
    private bool _isHostRequestedActive;
    private bool _isContentActive;
    private ILocalizationService? _localization;
    private MotionDirection _requestedDirection;

    public ExpandedModuleHost() => InitializeComponent();

    public event EventHandler? PreviousRequested;
    public event EventHandler? NextRequested;
    public event EventHandler? DefaultRequested;
    public event EventHandler<ModuleSelectedEventArgs>? ModuleSelected;
    public event EventHandler<ContentMotionRequestedEventArgs>? ContentMotionRequested;
    public event EventHandler<ModuleViewLoadFailedEventArgs>? ViewLoadFailed;

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

    public void ConfigureLocalization(ILocalizationService localization)
    {
        _localization = localization;
        Switcher.ConfigureLocalization(localization);
        UpdateHeader();
        foreach (var generic in _viewCache.Values.OfType<GenericExpandedModuleView>())
        {
            generic.ConfigureLocalization(localization);
        }
    }

    public void SetHostActive(bool isActive)
    {
        _isHostRequestedActive = isActive;
        UpdateContentActivation();
    }

    public void RefreshLocalizedContent()
    {
        Switcher.RefreshLocalizedContent();
        UpdateHeader();
    }

    private static void OnDisplayStateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var host = (ExpandedModuleHost)sender;
        if (host._requestedDirection == MotionDirection.None)
        {
            host._requestedDirection = host.ResolveDirection(
                args.OldValue as ModuleDisplayState,
                args.NewValue as ModuleDisplayState);
        }
        host.Switcher.SelectedModuleId = host.DisplayState?.Descriptor.Id;
        host.UpdateHeader();
        host.Render();
    }

    private static void OnAvailableModulesChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((ExpandedModuleHost)sender).Switcher.Modules = args.NewValue as IReadOnlyList<ModuleDisplayState>;

    private void UpdateHeader()
    {
        if (DisplayState is not { } state)
        {
            HeaderBar.Visibility = Visibility.Collapsed;
            return;
        }

        HeaderBar.Visibility = Visibility.Visible;
        HeaderGlyph.Glyph = state.Descriptor.IconGlyph;
        HeaderTitle.Text = LocalizedModuleName(state.Descriptor);
        HeaderStatus.Text = string.IsNullOrWhiteSpace(state.Presentation.ValueText)
            ? state.Presentation.PrimaryText
            : state.Presentation.ValueText;
        ToolTipService.SetToolTip(HeaderStatus, state.Presentation.SecondaryText);
        AutomationProperties.SetName(
            HeaderBar,
            $"{HeaderTitle.Text}, {state.Presentation.PrimaryText}");
    }

    private string LocalizedModuleName(ModuleDescriptor descriptor)
    {
        var value = _localization?.Get(descriptor.DisplayNameKey);
        return value is not null && value != descriptor.DisplayNameKey
            ? value
            : descriptor.DisplayName;
    }

    private void Render()
    {
        if (DisplayState is not { } state)
        {
            var idleContentChanged = _activeModuleId is not null ||
                                     _activeViewKey != IdleExpandedViewKey;
            if (_activeViewKey != IdleExpandedViewKey || ViewHost.Content is null)
            {
                DeactivateCurrentContent();
                ViewHost.Content = GetOrCreateView(
                    IdleExpandedViewKey,
                    static () => new IdleExpandedView());
                _activeViewKey = IdleExpandedViewKey;
            }

            _activeModuleId = null;
            UpdateContentActivation();
            RequestContentMotionIfNeeded(idleContentChanged);
            return;
        }

        var key = state.Descriptor.ExpandedViewKey;
        var contentChanged = !string.Equals(
            _activeModuleId,
            state.Descriptor.Id,
            StringComparison.Ordinal);
        if (_activeViewKey != key || ViewHost.Content is null)
        {
            DeactivateCurrentContent();
            ViewHost.Content = GetOrCreateView(
                key,
                () => new GenericExpandedModuleView(_localization));
            _activeViewKey = key;
        }

        _activeModuleId = state.Descriptor.Id;

        if (ViewHost.Content is GenericExpandedModuleView generic)
        {
            generic.DataContext = state.Presentation;
        }

        UpdateContentActivation();
        RequestContentMotionIfNeeded(contentChanged);
    }

    private void RequestContentMotionIfNeeded(bool contentChanged)
    {
        var direction = _requestedDirection;
        _requestedDirection = MotionDirection.None;
        if (!contentChanged && direction == MotionDirection.None)
        {
            return;
        }

        ContentMotionRequested?.Invoke(
            this,
            new ContentMotionRequestedEventArgs(ViewHost, direction));
    }

    private void OnPreviousRequested(object? sender, EventArgs args)
    {
        _requestedDirection = MotionDirection.Previous;
        PreviousRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnNextRequested(object? sender, EventArgs args)
    {
        _requestedDirection = MotionDirection.Next;
        NextRequested?.Invoke(this, EventArgs.Empty);
    }
    private void OnDefaultRequested(object? sender, EventArgs args) => DefaultRequested?.Invoke(this, EventArgs.Empty);

    private void OnModuleSelected(object? sender, ModuleSelectedEventArgs args) => ModuleSelected?.Invoke(this, args);

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

    private MotionDirection ResolveDirection(
        ModuleDisplayState? previous,
        ModuleDisplayState? current)
    {
        if (previous is null || current is null || AvailableModules is null)
        {
            return MotionDirection.None;
        }

        var previousIndex = -1;
        var currentIndex = -1;
        for (var index = 0; index < AvailableModules.Count; index++)
        {
            var moduleId = AvailableModules[index].Descriptor.Id;
            if (moduleId == previous.Descriptor.Id)
            {
                previousIndex = index;
            }
            if (moduleId == current.Descriptor.Id)
            {
                currentIndex = index;
            }
        }

        return previousIndex < 0 || currentIndex < 0 || previousIndex == currentIndex
            ? MotionDirection.None
            : currentIndex > previousIndex
                ? MotionDirection.Next
                : MotionDirection.Previous;
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
            view = CreateSafeFallbackView();
        }

        _viewCache.Add(viewKey, view);
        return view;
    }

    private FrameworkElement CreateSafeFallbackView()
    {
        var title = _localization?.Get("Dock.ModuleUnavailable");
        if (string.IsNullOrWhiteSpace(title) || title == "Dock.ModuleUnavailable")
        {
            title = "Bu bölüm şu anda kullanılamıyor";
        }

        return new Border
        {
            Margin = new Thickness(16, 8, 16, 12),
            Padding = new Thickness(18),
            CornerRadius = new CornerRadius(16),
            Child = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE783", FontSize = 22 },
                    new TextBlock
                    {
                        Text = title,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };
    }
}

public sealed record ContentMotionRequestedEventArgs(
    FrameworkElement Target,
    MotionDirection Direction);

public sealed record ModuleViewLoadFailedEventArgs(
    string ViewKey,
    Exception Exception);
