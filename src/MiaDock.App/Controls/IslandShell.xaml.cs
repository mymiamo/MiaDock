using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Composition;
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
    private const long ParallaxThrottleMilliseconds = 16;
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
    private ContentMotionRequestedEventArgs? _pendingContentMotion;
    private IslandVisualMetrics? _appliedMetrics;
    private long _lastParallaxTimestamp;
    private Vector3 _lastParallaxTranslation;
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

    public DockCornerRadii VisualCornerRadii =>
        _appliedMetrics?.CornerRadii ?? _layoutOptions.EffectiveCornerRadii;

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
        Surface.BorderThickness = new Thickness(0);
        Surface.Shadow = appearance.ShadowIntensity > 0.01 ? new ThemeShadow() : null;
        Surface.Translation = appearance.ShadowIntensity > 0.01
            ? new Vector3(0, 0, (float)(8 + appearance.ShadowIntensity * 24))
            : Vector3.Zero;

        ApplyCornerRadii(appearance.EffectiveCornerRadii);
    }

    public void ApplyCornerRadii(DockCornerRadii radii)
    {
        _baseLayoutOptions = _baseLayoutOptions with
        {
            CornerRadius = radii.TopLeft,
            CornerRadii = radii
        };
        _layoutOptions = CreateEffectiveLayout(_baseLayoutOptions, ModuleDisplay);
        if (_animationCoordinator is not null)
        {
            _animationCoordinator.UpdateOptions(_motionOptions, _layoutOptions);
            return;
        }

        ApplyMetrics(IslandAnimationProfile.ForState(_activeState, _layoutOptions));
    }

    public void ApplySystemBackdrop(ThemeStyle theme)
    {
        BackdropSurface.SystemBackdrop = theme switch
        {
            ThemeStyle.Windows11Mica => new MicaBackdrop { Kind = MicaKind.Base },
            ThemeStyle.Windows11MicaAlt => new MicaBackdrop { Kind = MicaKind.BaseAlt },
            ThemeStyle.AdaptiveFluent => new MicaBackdrop { Kind = MicaKind.Base },
            ThemeStyle.Windows11Acrylic or ThemeStyle.Windows11AcrylicThin => new DesktopAcrylicBackdrop(),
            ThemeStyle.BlurredGlass or ThemeStyle.NeutralFrostedGlass =>
                ColorlessGlassBackdrop.IsSupported ? new ColorlessGlassBackdrop() : null,
            _ => null
        };
    }

    public void ClearSystemBackdrop()
    {
        try
        {
            BackdropSurface.SystemBackdrop = null;
        }
        catch (Exception)
        {
            // Disconnecting Acrylic/Mica during HWND teardown can throw; the
            // element is about to disappear with the window either way.
        }
    }

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
        Surface.BorderThickness = new Thickness(0);
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

        shell._pendingContentMotion = null;
        var display = args.NewValue as ModuleDisplayState;
        shell._collapsedView.DisplayState = display;
        shell._hoverView.DisplayState = display;
        shell._expandedView.DisplayState = display;
        shell._notificationView.DisplayState = display;
        var contentMotion = shell._pendingContentMotion;
        shell._pendingContentMotion = null;
        shell.RefreshEffectiveLayout(contentMotion);
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
        _pendingContentMotion = args;

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

        var now = Environment.TickCount64;
        if (_lastParallaxTimestamp != 0 &&
            now - _lastParallaxTimestamp < ParallaxThrottleMilliseconds)
        {
            return;
        }

        _lastParallaxTimestamp = now;

        var point = args.GetCurrentPoint(LayoutRoot).Position;
        var width = Math.Max(LayoutRoot.ActualWidth, 1);
        var height = Math.Max(LayoutRoot.ActualHeight, 1);
        var limit = 3f * (float)_motionOptions.Intensity;
        var translation = new Vector3(
            ((float)(point.X / width) - 0.5f) * limit * 2,
            ((float)(point.Y / height) - 0.5f) * limit * 2,
            0);
        if (Vector3.DistanceSquared(_lastParallaxTranslation, translation) < 0.01f)
        {
            return;
        }

        _lastParallaxTranslation = translation;
        Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.SetIsTranslationEnabled(_expandedView, true);
        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(_expandedView);
        visual.Properties.InsertVector3("Translation", translation);
    }

    private void OnParallaxPointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs args) =>
        ResetParallax();

    private void ResetParallax()
    {
        _lastParallaxTimestamp = 0;
        if (_lastParallaxTranslation == Vector3.Zero)
        {
            return;
        }

        _lastParallaxTranslation = Vector3.Zero;
        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(_expandedView);
        visual.Properties.InsertVector3("Translation", Vector3.Zero);
    }

    private void ApplyMetrics(IslandVisualMetrics metrics)
    {
        if (_appliedMetrics is { } previous && MetricsAreEquivalent(previous, metrics))
        {
            return;
        }

        var sizeChanged = _appliedMetrics is not { } applied ||
                          !NearlyEqual(applied.Width, metrics.Width) ||
                          !NearlyEqual(applied.Height, metrics.Height);
        var radiusChanged = _appliedMetrics is not { } current ||
                            !RadiiAreEquivalent(current.CornerRadii, metrics.CornerRadii);
        if (sizeChanged)
        {
            LayoutRoot.Width = metrics.Width;
            LayoutRoot.Height = metrics.Height;
        }

        ClearHardClips();
        if (sizeChanged || radiusChanged)
        {
            var cornerRadius = ToXamlCornerRadius(metrics.CornerRadii);
            BackdropSurface.CornerRadius = cornerRadius;
            Surface.CornerRadius = cornerRadius;
        }

        _appliedMetrics = metrics;
        VisualSizeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RefreshEffectiveLayout(ContentMotionRequestedEventArgs? contentMotion)
    {
        var effective = CreateEffectiveLayout(_baseLayoutOptions, ModuleDisplay);
        var layoutChanged = effective != _layoutOptions;
        _layoutOptions = effective;
        if (contentMotion is not null && _activeState == IslandVisualState.ExpandedModule)
        {
            if (layoutChanged)
            {
                _animationCoordinator?.RequestModuleTransition(
                    contentMotion.Target,
                    contentMotion.Direction,
                    effective);
            }
            else
            {
                _animationCoordinator?.RequestContentTransition(
                    contentMotion.Target,
                    contentMotion.Direction);
            }

            AnimateBackdropEmphasis();
            return;
        }

        if (layoutChanged)
        {
            _animationCoordinator?.RequestLayoutTransition(effective);
        }
    }

    private static bool MetricsAreEquivalent(IslandVisualMetrics left, IslandVisualMetrics right) =>
        NearlyEqual(left.Width, right.Width) &&
        NearlyEqual(left.Height, right.Height) &&
        RadiiAreEquivalent(left.CornerRadii, right.CornerRadii);

    private void ClearHardClips()
    {
        // Geometric clips and rectangular XAML clips cut the silhouette without
        // anti-aliasing. The per-corner CornerRadius on BackdropSurface and
        // Surface already shapes the dock, so no additional mask is applied.
        LayoutRoot.Clip = null;
        Microsoft.UI.Xaml.Hosting.ElementCompositionPreview
            .GetElementVisual(LayoutRoot)
            .Clip = null;
    }

    private static CornerRadius ToXamlCornerRadius(DockCornerRadii radii) => new(
        radii.TopLeft,
        radii.TopRight,
        radii.BottomRight,
        radii.BottomLeft);

    private static bool RadiiAreEquivalent(DockCornerRadii left, DockCornerRadii right) =>
        NearlyEqual(left.TopLeft, right.TopLeft) &&
        NearlyEqual(left.TopRight, right.TopRight) &&
        NearlyEqual(left.BottomRight, right.BottomRight) &&
        NearlyEqual(left.BottomLeft, right.BottomLeft);

    private static bool NearlyEqual(double left, double right) => Math.Abs(left - right) < 0.01;

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
