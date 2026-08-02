using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Composition.SystemBackdrops;
using MiaDock.App.Animations;
using MiaDock.App.Services;
using MiaDock.Core.Overlay;
using MiaDock.Core.Presentation;
using MiaDock.Core.Theming;
using Windows.UI;
using MiaDock.Core.Settings;
using MiaDock.Core.Modules;
using MiaDock.Core.Localization;
using System.Numerics;
using MiaDock.UI.Services;

namespace MiaDock.App.Controls;

public sealed partial class IslandShell : UserControl
{
    private readonly CompactModuleHost _collapsedView;
    private readonly CompactModuleHost _hoverView;
    private readonly ExpandedModuleHost _expandedView;
    private readonly ModuleNotificationHost _notificationView;
    private readonly IReadOnlyDictionary<IslandVisualState, FrameworkElement> _views;
    private IIslandAnimationCoordinator? _animationCoordinator;
    private IslandLayoutOptions _layoutOptions = IslandLayoutOptions.Default;
    private IslandLayoutOptions _baseLayoutOptions = IslandLayoutOptions.Default;
    private IslandMotionOptions _motionOptions = IslandMotionOptions.Default;
    private IAnimationPreferenceService? _animationPreferences;
    private IslandVisualState _activeState = IslandVisualState.Collapsed;
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State),
        typeof(string),
        typeof(IslandShell),
        new PropertyMetadata(nameof(IslandVisualState.Collapsed), OnStateChanged));

    public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register(
        nameof(Theme),
        typeof(string),
        typeof(IslandShell),
        new PropertyMetadata(string.Empty, OnThemeChanged));

    public static readonly DependencyProperty ModuleDisplayProperty = DependencyProperty.Register(
        nameof(ModuleDisplay),
        typeof(ModuleDisplayState),
        typeof(IslandShell),
        new PropertyMetadata(null, OnModuleDisplayChanged));

    public static readonly DependencyProperty AvailableModulesProperty = DependencyProperty.Register(
        nameof(AvailableModules),
        typeof(IReadOnlyList<ModuleDisplayState>),
        typeof(IslandShell),
        new PropertyMetadata(null, OnAvailableModulesChanged));

    public static readonly DependencyProperty ShowNotificationControlsProperty = DependencyProperty.Register(
        nameof(ShowNotificationControls), typeof(bool), typeof(IslandShell),
        new PropertyMetadata(false, OnShowNotificationControlsChanged));

    public IslandShell()
    {
        InitializeComponent();

        _collapsedView = new CompactModuleHost();
        _hoverView = new CompactModuleHost { UseHoverView = true };
        _expandedView = new ExpandedModuleHost();
        _notificationView = new ModuleNotificationHost();

        _views = new Dictionary<IslandVisualState, FrameworkElement>
        {
            [IslandVisualState.Collapsed] = _collapsedView,
            [IslandVisualState.Hover] = _hoverView,
            [IslandVisualState.ExpandedModule] = _expandedView,
            [IslandVisualState.ModuleNotification] = _notificationView
        };

        ContentHost.Children.Add(_collapsedView);
        ContentHost.Children.Add(_hoverView);
        ContentHost.Children.Add(_expandedView);
        ContentHost.Children.Add(_notificationView);

        _expandedView.PreviousRequested += OnPreviousModuleRequested;
        _expandedView.NextRequested += OnNextModuleRequested;
        _expandedView.DefaultRequested += OnDefaultModuleRequested;
        _expandedView.ModuleSelected += OnModuleSelected;
        _expandedView.ContentMotionRequested += OnContentMotionRequested;
        _collapsedView.ViewLoadFailed += OnViewLoadFailed;
        _hoverView.ViewLoadFailed += OnViewLoadFailed;
        _expandedView.ViewLoadFailed += OnViewLoadFailed;
        _notificationView.ViewLoadFailed += OnViewLoadFailed;
        PointerMoved += OnParallaxPointerMoved;
        PointerExited += OnParallaxPointerExited;

        ApplyTheme(Theme);
        ApplyState(State);
    }

    public string State
    {
        get => (string)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public string Theme
    {
        get => (string)GetValue(ThemeProperty);
        set => SetValue(ThemeProperty, value);
    }

    public OverlaySize VisualSize => new(LayoutRoot.Width, LayoutRoot.Height);

    public double VisualCornerRadius => Surface.CornerRadius.TopLeft;

    public event EventHandler? VisualSizeChanged;

    public void ConfigureAnimations(
        IAnimationPreferenceService animationPreferences,
        IslandMotionOptions options,
        IslandLayoutOptions layoutOptions,
        IslandVisualState initialState)
    {
        _baseLayoutOptions = layoutOptions;
        _layoutOptions = CreateEffectiveLayout(layoutOptions, ModuleDisplay);
        _motionOptions = options;
        _animationPreferences = animationPreferences;
        if (_animationCoordinator is null)
        {
            _animationCoordinator = new IslandAnimationCoordinator(
                LayoutRoot,
                Surface,
                _views,
                ApplyMetrics,
                animationPreferences,
                options,
                _layoutOptions);
            _animationCoordinator.ApplyInitialState(initialState);
        }
        else
        {
            _animationCoordinator.UpdateOptions(options, _layoutOptions);
        }
    }

    public ModuleDisplayState? ModuleDisplay
    {
        get => (ModuleDisplayState?)GetValue(ModuleDisplayProperty);
        set => SetValue(ModuleDisplayProperty, value);
    }

    public IReadOnlyList<ModuleDisplayState>? AvailableModules
    {
        get => (IReadOnlyList<ModuleDisplayState>?)GetValue(AvailableModulesProperty);
        set => SetValue(AvailableModulesProperty, value);
    }

    public bool ShowNotificationControls
    {
        get => (bool)GetValue(ShowNotificationControlsProperty);
        set => SetValue(ShowNotificationControlsProperty, value);
    }

    public event EventHandler? PreviousModuleRequested;

    public event EventHandler? NextModuleRequested;

    public event EventHandler? DefaultModuleRequested;

    public event EventHandler<ModuleSelectedEventArgs>? ModuleSelected;

    public event EventHandler<ModuleViewLoadFailedEventArgs>? ModuleViewLoadFailed;

    public void ConfigureModuleViews(IModuleViewRegistry viewRegistry)
    {
        ArgumentNullException.ThrowIfNull(viewRegistry);
        _collapsedView.Configure(viewRegistry);
        _hoverView.Configure(viewRegistry);
        _expandedView.Configure(viewRegistry);
        _notificationView.Configure(viewRegistry);
    }

    public void ConfigureLocalization(ILocalizationService localization)
    {
        _collapsedView.ConfigureLocalization(localization);
        _hoverView.ConfigureLocalization(localization);
        _expandedView.ConfigureLocalization(localization);
        _notificationView.ConfigureLocalization(localization);
    }

    public void RefreshLocalizedContent() => _expandedView.RefreshLocalizedContent();

    public void ApplyTransition(IslandTransition transition)
    {
        _activeState = transition.CurrentState;
        UpdateHostActivation(transition.CurrentState);
        if (!transition.Changed && transition.Trigger == IslandTrigger.ModuleEventReceived)
        {
            _animationCoordinator?.RequestContentRefresh(_views[transition.CurrentState]);
        }
        else
        {
            _animationCoordinator?.RequestTransition(transition);
        }

        AnimateBackdropEmphasis();
    }

    public void RefreshActiveContent() =>
        _animationCoordinator?.RequestContentRefresh(_expandedView);

    public void DisposeAnimations()
    {
        _animationCoordinator?.Dispose();
        _animationCoordinator = null;
    }

    public void ApplyAppearance(AppearanceSettings appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        var background = ColorParser.ParseRgb(appearance.BackgroundColor);
        var accent = ColorParser.ParseRgb(appearance.AccentColor);
        RequestedTheme = appearance.Theme is ThemeStyle.OledBlack || appearance.Theme.UsesColorlessGlass()
            ? ElementTheme.Dark
            : appearance.Theme is ThemeStyle.AppleLike or ThemeStyle.CustomSolidColor
            ? SolidThemeContrastPaletteFactory.Create(background, accent).Primary.R > 127
                ? ElementTheme.Dark
                : ElementTheme.Light
            : ElementTheme.Default;
        if (appearance.Theme == ThemeStyle.AdaptiveFluent)
        {
            Surface.ClearValue(Border.BackgroundProperty);
            Surface.ClearValue(Border.BorderBrushProperty);
        }
        else
        {
            Surface.Background = CreateSurfaceBrush(appearance.Theme, background, appearance.Opacity);
            Surface.BorderBrush = appearance.Theme.UsesColorlessGlass()
            ? new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF))
            : appearance.Theme.IsWindows11Style()
            ? new SolidColorBrush(Color.FromArgb(
                checked((byte)Math.Round(48 + appearance.ShadowIntensity * 96)),
                accent.R,
                accent.G,
                accent.B))
            : new SolidColorBrush(Color.FromArgb(
                checked((byte)Math.Round(255 - appearance.ShadowIntensity * 80)),
                37,
                37,
                37));
        }
        Surface.BorderThickness = new Thickness(1 + appearance.ShadowIntensity);
        Surface.Shadow = appearance.ShadowIntensity > 0.01 ? new ThemeShadow() : null;
        Surface.Translation = appearance.ShadowIntensity > 0.01
            ? new Vector3(0, 0, (float)(8 + appearance.ShadowIntensity * 24))
            : Vector3.Zero;
    }

    public void ApplySystemBackdrop(ThemeStyle theme)
    {
        BackdropSurface.SystemBackdrop = theme switch
        {
            ThemeStyle.Windows11Mica => new MicaBackdrop { Kind = MicaKind.Base },
            ThemeStyle.Windows11MicaAlt => new MicaBackdrop { Kind = MicaKind.BaseAlt },
            ThemeStyle.AdaptiveFluent => new MicaBackdrop { Kind = MicaKind.Base },
            ThemeStyle.Windows11Acrylic => new DesktopAcrylicBackdrop(),
            ThemeStyle.Windows11AcrylicThin or ThemeStyle.BlurredGlass or ThemeStyle.NeutralFrostedGlass =>
                new DesktopAcrylicBackdrop(),
            _ => null
        };
    }

    public void ClearSystemBackdrop() => BackdropSurface.SystemBackdrop = null;

    private static void OnStateChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is IslandShell shell && args.NewValue is string state)
        {
            shell.ApplyState(state);
        }
    }

    private static void OnThemeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is IslandShell shell)
        {
            shell.ApplyTheme(args.NewValue as string);
        }
    }

    private void ApplyTheme(string? themeName)
    {
        var theme = Enum.TryParse<ThemeStyle>(themeName, out var parsed)
            ? parsed
            : ThemeStyle.AppleLike;
        RequestedTheme = theme is ThemeStyle.AppleLike or ThemeStyle.OledBlack || theme.UsesColorlessGlass()
            ? ElementTheme.Dark
            : ElementTheme.Default;
        var fallback = theme.UsesColorlessGlass()
            ? Color.FromArgb(0xFF, 0x14, 0x14, 0x14)
            : theme.IsWindows11Style()
            ? Color.FromArgb(0xFF, 0x20, 0x21, 0x24)
            : Color.FromArgb(0xFF, 0x05, 0x05, 0x06);
        Surface.Background = CreateSurfaceBrush(theme, fallback, 1);

        Surface.BorderBrush = theme.UsesColorlessGlass()
            ? new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF))
            : new SolidColorBrush(Color.FromArgb(255, 37, 37, 37));
        Surface.BorderThickness = new Thickness(1);
    }

    private static Brush CreateSurfaceBrush(ThemeStyle theme, Color color, double opacity)
    {
        var normalizedOpacity = Math.Clamp(opacity, 0.35, 1);
        return theme switch
        {
            ThemeStyle.Windows11Mica => new SolidColorBrush(WithOpacity(color, 0.91 * normalizedOpacity)),
            ThemeStyle.Windows11MicaAlt => new SolidColorBrush(WithOpacity(color, 0.86 * normalizedOpacity)),
            ThemeStyle.Windows11Acrylic => CreateAcrylic(color, 0.72, normalizedOpacity),
            ThemeStyle.Windows11AcrylicThin => CreateAcrylic(color, 0.46, normalizedOpacity),
            ThemeStyle.BlurredGlass or ThemeStyle.NeutralFrostedGlass => CreateGlassOverlay(normalizedOpacity),
            ThemeStyle.OledBlack => new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
            _ => new SolidColorBrush(WithOpacity(color, normalizedOpacity))
        };
    }

    private static SolidColorBrush CreateGlassOverlay(double opacity) => new(Color.FromArgb(
        checked((byte)Math.Round(0.08 * opacity * byte.MaxValue)),
        0x14,
        0x14,
        0x14));

    private static AcrylicBrush CreateAcrylic(Color color, double tintOpacity, double opacity) => new()
    {
        FallbackColor = color,
        TintColor = color,
        TintOpacity = tintOpacity,
        Opacity = opacity
    };

    private static Color WithOpacity(Color color, double opacity) => Color.FromArgb(
        checked((byte)Math.Round(Math.Clamp(opacity, 0, 1) * byte.MaxValue)),
        color.R,
        color.G,
        color.B);

    private void ApplyState(string stateName)
    {
        if (!Enum.TryParse<IslandVisualState>(stateName, out var state))
        {
            state = IslandVisualState.Collapsed;
        }

        _activeState = state;
        ApplyMetrics(IslandAnimationProfile.ForState(state, _layoutOptions));

        _collapsedView.Visibility = state == IslandVisualState.Collapsed ? Visibility.Visible : Visibility.Collapsed;
        _hoverView.Visibility = state == IslandVisualState.Hover ? Visibility.Visible : Visibility.Collapsed;
        _expandedView.Visibility = state == IslandVisualState.ExpandedModule ? Visibility.Visible : Visibility.Collapsed;
        _notificationView.Visibility = state == IslandVisualState.ModuleNotification ? Visibility.Visible : Visibility.Collapsed;
        UpdateHostActivation(state);
    }

    private void UpdateHostActivation(IslandVisualState state)
    {
        _collapsedView.SetHostActive(state == IslandVisualState.Collapsed);
        _hoverView.SetHostActive(state == IslandVisualState.Hover);
        _expandedView.SetHostActive(state == IslandVisualState.ExpandedModule);
    }

    private static void OnModuleDisplayChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not IslandShell shell)
        {
            return;
        }

        var display = args.NewValue as ModuleDisplayState;
        shell._collapsedView.DisplayState = display;
        shell._hoverView.DisplayState = display;
        shell._expandedView.DisplayState = display;
        shell._notificationView.DisplayState = display;
        shell.RefreshEffectiveLayout();
    }

    private static void OnAvailableModulesChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args) =>
        ((IslandShell)dependencyObject)._expandedView.AvailableModules =
            args.NewValue as IReadOnlyList<ModuleDisplayState>;

    private static void OnShowNotificationControlsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args) =>
        ((IslandShell)dependencyObject)._notificationView.ShowControls = (bool)args.NewValue;

    private void OnPreviousModuleRequested(object? sender, EventArgs args) =>
        PreviousModuleRequested?.Invoke(this, EventArgs.Empty);

    private void OnNextModuleRequested(object? sender, EventArgs args) =>
        NextModuleRequested?.Invoke(this, EventArgs.Empty);

    private void OnDefaultModuleRequested(object? sender, EventArgs args) =>
        DefaultModuleRequested?.Invoke(this, EventArgs.Empty);

    private void OnModuleSelected(object? sender, ModuleSelectedEventArgs args) =>
        ModuleSelected?.Invoke(this, args);

    private void OnViewLoadFailed(object? sender, ModuleViewLoadFailedEventArgs args) =>
        ModuleViewLoadFailed?.Invoke(this, args);

    private void OnContentMotionRequested(object? sender, ContentMotionRequestedEventArgs args) =>
        RequestContentMotion(args);

    private void RequestContentMotion(ContentMotionRequestedEventArgs args)
    {
        _animationCoordinator?.RequestContentTransition(args.Target, args.Direction);
        AnimateBackdropEmphasis();
    }

    private void AnimateBackdropEmphasis()
    {
        if (!_motionOptions.EnableTransientBlur ||
            _animationPreferences?.AnimationsEnabled != true ||
            _motionOptions.Preset == MotionPreset.Off)
        {
            return;
        }

        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(BackdropSurface);
        visual.StopAnimation(nameof(visual.Opacity));
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = _motionOptions.ContentRefreshDuration;
        animation.InsertKeyFrame(0, 0.78f);
        animation.InsertKeyFrame(1, 1f);
        visual.StartAnimation(nameof(visual.Opacity), animation);
    }

    private void OnParallaxPointerMoved(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args)
    {
        if (!_motionOptions.EnableParallax ||
            State != nameof(IslandVisualState.ExpandedModule))
        {
            ResetParallax();
            return;
        }

        var point = args.GetCurrentPoint(LayoutRoot).Position;
        var width = Math.Max(LayoutRoot.ActualWidth, 1);
        var height = Math.Max(LayoutRoot.ActualHeight, 1);
        var limit = 3f * (float)_motionOptions.Intensity;
        Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.SetIsTranslationEnabled(_expandedView, true);
        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(_expandedView);
        visual.Properties.InsertVector3("Translation", new Vector3(
            ((float)(point.X / width) - 0.5f) * limit * 2,
            ((float)(point.Y / height) - 0.5f) * limit * 2,
            0));
    }

    private void OnParallaxPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args) =>
        ResetParallax();

    private void ResetParallax()
    {
        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(_expandedView);
        visual.Properties.InsertVector3("Translation", Vector3.Zero);
    }

    private void ApplyMetrics(IslandVisualMetrics metrics)
    {
        LayoutRoot.Width = metrics.Width;
        LayoutRoot.Height = metrics.Height;
        var rootVisual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(LayoutRoot);
        var clipGeometry = rootVisual.Compositor.CreateRoundedRectangleGeometry();
        clipGeometry.Size = new Vector2((float)metrics.Width, (float)metrics.Height);
        clipGeometry.CornerRadius = new Vector2((float)metrics.CornerRadius);
        rootVisual.Clip = rootVisual.Compositor.CreateGeometricClip(clipGeometry);
        BackdropSurface.CornerRadius = new CornerRadius(metrics.CornerRadius);
        Surface.CornerRadius = new CornerRadius(metrics.CornerRadius);
        VisualSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshEffectiveLayout()
    {
        var effective = CreateEffectiveLayout(_baseLayoutOptions, ModuleDisplay);
        if (effective == _layoutOptions)
        {
            return;
        }

        _layoutOptions = effective;
        _animationCoordinator?.RequestLayoutTransition(effective);
    }

    private static IslandLayoutOptions CreateEffectiveLayout(
        IslandLayoutOptions baseLayout,
        ModuleDisplayState? display)
    {
        var minimum = display?.Descriptor.MinimumExpandedHeight ?? 300;
        return baseLayout with
        {
            ExpandedHeight = Math.Clamp(Math.Max(baseLayout.ExpandedHeight, minimum), 360, 420)
        };
    }
}
