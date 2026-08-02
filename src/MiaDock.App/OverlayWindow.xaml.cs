using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using MiaDock.App.Services;
using MiaDock.App.ViewModels;
using MiaDock.Core.Presentation;
using MiaDock.Core.Settings;
using MiaDock.Modules.Media.Services;
using MiaDock.Platform.Windows.Overlay;
using MiaDock.UI.Services;
using MiaDock.Core.Modules;
using MiaDock.Platform.Windows.Display;
using MiaDock.Platform.Windows.Fullscreen;
using MiaDock.Core.Logging;
using MiaDock.App.Infrastructure;
using MiaDock.Platform.Windows.Lifecycle;
using MiaDock.Core.Theming;
using MiaDock.Modules.Time.Services;
using MiaDock.Core.Focus;
using Windows.UI;
using WinRT;

namespace MiaDock.App;

public sealed partial class OverlayWindow : Window
{
    private const long WheelNavigationThrottleMilliseconds = 90;
    private readonly OverlayWindowViewModel _viewModel;
    private readonly IOverlayWindowController _windowController;
    private readonly IslandAutoCollapseController _autoCollapse;
    private readonly DispatcherQueueTimer _moduleReturnTimer;
    private readonly IAnimationPreferenceService _animationPreferences;
    private readonly ISettingsService _settings;
    private readonly ISettingsWindowService _settingsWindow;
    private readonly IThemeService _themeService;
    private readonly IMediaSessionService _media;
    private readonly IFullscreenDetectionService _fullscreen;
    private readonly IDisplayTopologyService _displayTopology;
    private readonly IIslandModuleRegistry _moduleRegistry;
    private readonly IWindowsSessionLockStateService _sessionLockState;
    private readonly ISystemResumeService _systemResume;
    private readonly PresentationPrivacyPolicy _privacyPolicy;
    private readonly IFocusPolicyService _focusPolicy;
    private readonly ILogService _log;
    private readonly IAppLocalizationService _localization;
    private DesktopAcrylicController? _glassAcrylicController;
    private SystemBackdropConfiguration? _glassBackdropConfiguration;
    private ICompositionSupportsSystemBackdrop? _windowBackdropTarget;
    private Windows.UI.Composition.Compositor? _transparentBackdropCompositor;
    private Windows.UI.Composition.CompositionColorBrush? _transparentWindowBackdrop;
    private CancellationTokenSource? _mediaSelectionCancellation;
    private AppearanceSettings? _appliedAppearance;
    private IslandLayoutOptions? _appliedLayoutOptions;
    private IslandMotionOptions? _appliedMotionOptions;
    private FullscreenSnapshot _fullscreenState = FullscreenSnapshot.None;
    private bool _temporaryNotificationVisible;
    private bool _manuallyHidden = true;
    private long _lastWheelNavigationTimestamp;

    public OverlayWindow(
        OverlayWindowViewModel viewModel,
        IOverlayWindowControllerFactory controllerFactory,
        IAnimationPreferenceService animationPreferences,
        ISettingsService settings,
        ISettingsWindowService settingsWindow,
        IThemeService themeService,
        IMediaSessionService media,
        IFullscreenDetectionService fullscreen,
        IDisplayTopologyService displayTopology,
        IIslandModuleRegistry moduleRegistry,
        IModuleViewRegistry moduleViews,
        ILogService log,
        IWindowsSessionLockStateService sessionLockState,
        ISystemResumeService systemResume,
        PresentationPrivacyPolicy privacyPolicy,
        IFocusPolicyService focusPolicy,
        IAppLocalizationService localization)
    {
        InitializeComponent();
        WindowBranding.ApplyIcon(this);
        _viewModel = viewModel;
        _animationPreferences = animationPreferences;
        _settings = settings;
        _settingsWindow = settingsWindow;
        _themeService = themeService;
        _media = media;
        _fullscreen = fullscreen;
        _displayTopology = displayTopology;
        _moduleRegistry = moduleRegistry;
        _log = log;
        _sessionLockState = sessionLockState;
        _systemResume = systemResume;
        _privacyPolicy = privacyPolicy;
        _focusPolicy = focusPolicy;
        _localization = localization;
        Root.DataContext = viewModel;
        ApplyTransparentWindowBackdrop();
        Island.ConfigureModuleViews(moduleViews);
        Island.ConfigureLocalization(localization);
        ApplyLocalization();

        _windowController = controllerFactory.Create(this, OverlayWindowOptions.Default);
        _windowController.OutsidePointerPressed += OnOutsidePointerPressed;
        var motionOptions = SettingsMapper.ToMotionOptions(settings.Current);
        _autoCollapse = new IslandAutoCollapseController(DispatcherQueue, motionOptions);
        _autoCollapse.Elapsed += OnAutoCollapseElapsed;
        _moduleReturnTimer = DispatcherQueue.CreateTimer();
        _moduleReturnTimer.IsRepeating = false;
        _moduleReturnTimer.Tick += OnModuleReturnTimerTick;
        _viewModel.Island.UpdateTemporarySelectionDuration(
            TimeSpan.FromSeconds(settings.Current.General.PassiveModuleReturnSeconds));
        ApplyAppearance(settings.Current);

        Island.VisualSizeChanged += OnIslandVisualSizeChanged;
        _viewModel.Island.TransitionRequested += OnTransitionRequested;
        _viewModel.Island.PropertyChanged += OnIslandPropertyChanged;
        _settings.SettingsChanged += OnSettingsChanged;
        _fullscreen.StateChanged += OnFullscreenStateChanged;
        _displayTopology.DisplaysChanged += OnDisplaysChanged;
        _moduleRegistry.ModuleEventOccurred += OnModuleEventOccurred;
        _sessionLockState.LockStateChanged += OnSessionLockStateChanged;
        _systemResume.Resumed += OnSystemResumed;
        _focusPolicy.PolicyChanged += OnFocusPolicyChanged;
        _localization.LanguageChanged += OnLanguageChanged;
        _themeService.ThemeEnvironmentChanged += OnThemeEnvironmentChanged;
        DockInteractionSession.ActivityChanged += OnDockInteractionActivityChanged;
        Closed += OnClosed;
        _fullscreen.Start();
        _fullscreenState = _fullscreen.Current;
        RefreshModuleReturnTimer();
        ApplyEnvironment();
    }

    public void ShowNoActivate()
    {
        _manuallyHidden = false;
        _windowController.UpdateLayout(Island.VisualSize, Island.VisualCornerRadius);
        ApplyEnvironment();
    }

    public bool IsDockVisible => _windowController.IsVisible;

    public void ShowDock()
    {
        _manuallyHidden = false;
        ApplyEnvironment();
    }

    public void HideDock()
    {
        _manuallyHidden = true;
        _windowController.Hide();
    }

    public void ToggleDock()
    {
        if (_windowController.IsVisible)
        {
            HideDock();
        }
        else
        {
            ShowDock();
        }
    }

    public void ToggleExpandedFromShortcut()
    {
        _manuallyHidden = false;
        _viewModel.Island.HandlePrimaryInvoked();
        RegisterDockActivity();
        ApplyEnvironment();
    }

    public void SelectNextModuleFromShortcut()
    {
        _manuallyHidden = false;
        TryRunDockAction(
            _viewModel.Island.SelectNextModule,
            "shortcut-next-module");
        ApplyEnvironment();
    }

    private void OnIslandPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        _autoCollapse.PointerEntered();
        if (_settings.Current.General.InteractionMode is IslandInteractionMode.Hover or IslandInteractionMode.HoverAndClick)
        {
            _viewModel.Island.HandlePointerEntered();
        }

        RegisterDockActivity();
    }

    private void OnIslandPointerExited(object sender, PointerRoutedEventArgs args)
    {
        _autoCollapse.PointerExited();
    }

    private void OnIslandPointerMoved(object sender, PointerRoutedEventArgs args) =>
        RegisterDockActivity();

    private void OnIslandPointerWheelChanged(object sender, PointerRoutedEventArgs args)
    {
        var delta = args.GetCurrentPoint(Island).Properties.MouseWheelDelta;
        if (delta == 0)
        {
            return;
        }

        args.Handled = true;
        var now = Environment.TickCount64;
        if (_lastWheelNavigationTimestamp != 0 &&
            now - _lastWheelNavigationTimestamp < WheelNavigationThrottleMilliseconds)
        {
            RegisterDockActivity();
            return;
        }

        _lastWheelNavigationTimestamp = now;
        try
        {
            if (delta > 0)
            {
                _viewModel.Island.SelectPreviousModule();
            }
            else
            {
                _viewModel.Island.SelectNextModule();
            }
        }
        catch (Exception exception)
        {
            _log.Write(
                TechnicalLogLevel.Error,
                TechnicalEventIds.ModuleNavigationFailed,
                "Overlay",
                "Dock module navigation failed.",
                exception,
                new Dictionary<string, object?> { ["input"] = "wheel" });
        }

        RegisterDockActivity();
    }

    private void OnIslandTapped(object sender, TappedRoutedEventArgs args)
    {
        if (IsInteractiveTapSource(args.OriginalSource as DependencyObject))
        {
            // Controls inside compact/hover/expanded content own the gesture.
            // Do not reinterpret their click as a request to expand the island.
            args.Handled = true;
            RegisterDockActivity();
            return;
        }

        if (_viewModel.Island.CurrentState != IslandVisualState.ExpandedModule &&
            _settings.Current.General.InteractionMode is IslandInteractionMode.Click or IslandInteractionMode.HoverAndClick)
        {
            _viewModel.Island.HandlePrimaryInvoked();
        }

        RegisterDockActivity();
    }

    private bool IsInteractiveTapSource(DependencyObject? source)
    {
        for (var current = source; current is not null && !ReferenceEquals(current, Island);)
        {
            if (current is ButtonBase or RangeBase or ToggleSwitch or
                ComboBox or TextBox or RichEditBox or ListViewBase or ScrollViewer)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private void OnOutsidePointerPressed(object? sender, EventArgs args)
    {
        if (DockInteractionSession.IsActive ||
            _viewModel.Island.CurrentState != IslandVisualState.ExpandedModule)
        {
            return;
        }

        _viewModel.Island.HandlePointerExited();
        _viewModel.Island.HandleCollapseRequested();
        _temporaryNotificationVisible = false;
        ApplyEnvironment();
    }

    private void OnIslandKeyDown(object sender, KeyRoutedEventArgs args) =>
        RegisterDockActivity();

    private void OnSettingsClick(object sender, RoutedEventArgs args) =>
        TryRunDockAction(_settingsWindow.Show, "open-settings");

    private void OnPreviousModuleRequested(object? sender, EventArgs args)
    {
        TryRunDockAction(
            _viewModel.Island.SelectPreviousModule,
            "previous-module");
    }

    private void OnNextModuleRequested(object? sender, EventArgs args)
    {
        TryRunDockAction(
            _viewModel.Island.SelectNextModule,
            "next-module");
    }

    private void OnDefaultModuleRequested(object? sender, EventArgs args)
    {
        TryRunDockAction(
            _viewModel.Island.SelectDefault,
            "default-module");
    }

    private void OnModuleSelected(object? sender, Controls.ModuleSelectedEventArgs args)
    {
        TryRunDockAction(
            () => _viewModel.Island.SelectModule(args.ModuleId),
            $"select-module:{args.ModuleId}");
    }

    private void OnModuleViewLoadFailed(
        object? sender,
        Controls.ModuleViewLoadFailedEventArgs args) =>
        _log.Write(
            TechnicalLogLevel.Error,
            TechnicalEventIds.ModuleNavigationFailed,
            "Overlay",
            "A dock module view failed to load; a safe fallback was shown.",
            args.Exception,
            new Dictionary<string, object?> { ["viewKey"] = args.ViewKey });

    private void TryRunDockAction(Action action, string operation)
    {
        try
        {
            action();
            RegisterDockActivity();
        }
        catch (Exception exception)
        {
            _log.Write(
                TechnicalLogLevel.Error,
                TechnicalEventIds.ModuleNavigationFailed,
                "Overlay",
                "A dock interaction failed safely.",
                exception,
                new Dictionary<string, object?> { ["operation"] = operation });
        }
    }

    private void RegisterDockActivity()
    {
        _autoCollapse.RegisterActivity(_viewModel.Island.CurrentState);
        _viewModel.Island.NotifyModuleInteraction();
        RefreshModuleReturnTimer();
    }

    private void OnIslandPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(_viewModel.Island.TemporarySelectionExpiresAtUtc))
        {
            return;
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            RefreshModuleReturnTimer();
        }
        else
        {
            DispatcherQueue.TryEnqueue(RefreshModuleReturnTimer);
        }
    }

    private void RefreshModuleReturnTimer()
    {
        _moduleReturnTimer.Stop();
        if (_viewModel.Island.TemporarySelectionExpiresAtUtc is not { } expiresAtUtc)
        {
            return;
        }

        var remaining = expiresAtUtc - DateTimeOffset.UtcNow;
        _moduleReturnTimer.Interval = remaining > TimeSpan.Zero
            ? remaining
            : TimeSpan.FromMilliseconds(1);
        _moduleReturnTimer.Start();
    }

    private void OnModuleReturnTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        if (_viewModel.Island.ExpireTemporarySelection())
        {
            _viewModel.Island.HandleInactivityElapsed();
            ApplyEnvironment();
        }

        RefreshModuleReturnTimer();
    }

    private void OnIslandVisualSizeChanged(object? sender, EventArgs args) =>
        _windowController.UpdateLayout(Island.VisualSize, Island.VisualCornerRadius);

    private void OnTransitionRequested(object? sender, IslandTransition transition)
    {
        var isExpanded = transition.CurrentState == IslandVisualState.ExpandedModule;
        _windowController.SetInputActivationEnabled(isExpanded);
        _windowController.SetOutsideClickMonitoring(
            isExpanded && !DockInteractionSession.IsActive);

        if (transition.CurrentState == IslandVisualState.ModuleNotification)
        {
            _autoCollapse.SetNotificationDuration(_viewModel.Island.ActiveEventDisplayDuration);
        }

        Island.ApplyTransition(transition);
        _autoCollapse.ObserveTransition(transition);
    }

    private void OnDockInteractionActivityChanged(object? sender, bool isActive)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            ApplyDockInteractionActivity(isActive);
        }
        else
        {
            DispatcherQueue.TryEnqueue(() => ApplyDockInteractionActivity(isActive));
        }
    }

    private void ApplyDockInteractionActivity(bool isActive)
    {
        if (isActive)
        {
            _autoCollapse.SuspendTransientInteraction();
            _windowController.SetOutsideClickMonitoring(false);
            return;
        }

        var state = _viewModel.Island.CurrentState;
        _autoCollapse.ResumeTransientInteraction(state);
        _windowController.SetOutsideClickMonitoring(
            state == IslandVisualState.ExpandedModule);
    }

    private void OnAutoCollapseElapsed(object? sender, IslandTrigger trigger)
    {
        if (trigger == IslandTrigger.PointerExited)
        {
            _viewModel.Island.HandlePointerExited();
        }
        else if (trigger == IslandTrigger.NotificationElapsed)
        {
            _viewModel.Island.HandleNotificationElapsed();
        }
        else if (trigger == IslandTrigger.InactivityElapsed)
        {
            _viewModel.Island.HandleInactivityElapsed();
        }

        if (_viewModel.Island.CurrentState == IslandVisualState.Collapsed)
        {
            _temporaryNotificationVisible = false;
        }

        ApplyEnvironment();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _autoCollapse.Elapsed -= OnAutoCollapseElapsed;
        _autoCollapse.Dispose();
        _moduleReturnTimer.Stop();
        _moduleReturnTimer.Tick -= OnModuleReturnTimerTick;
        Island.VisualSizeChanged -= OnIslandVisualSizeChanged;
        Island.DisposeAnimations();
        _viewModel.Island.TransitionRequested -= OnTransitionRequested;
        _viewModel.Island.PropertyChanged -= OnIslandPropertyChanged;
        _settings.SettingsChanged -= OnSettingsChanged;
        _fullscreen.StateChanged -= OnFullscreenStateChanged;
        _displayTopology.DisplaysChanged -= OnDisplaysChanged;
        _moduleRegistry.ModuleEventOccurred -= OnModuleEventOccurred;
        _sessionLockState.LockStateChanged -= OnSessionLockStateChanged;
        _systemResume.Resumed -= OnSystemResumed;
        _focusPolicy.PolicyChanged -= OnFocusPolicyChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
        _themeService.ThemeEnvironmentChanged -= OnThemeEnvironmentChanged;
        DockInteractionSession.ActivityChanged -= OnDockInteractionActivityChanged;
        _windowController.OutsidePointerPressed -= OnOutsidePointerPressed;
        _fullscreen.Dispose();
        _mediaSelectionCancellation?.Cancel();
        _mediaSelectionCancellation?.Dispose();
        Island.ClearSystemBackdrop();
        ClearColorlessAcrylicBackdrop();
        ClearTransparentWindowBackdrop();
        Closed -= OnClosed;
        _windowController.Dispose();
    }

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            ApplyLocalization();
        }
        else
        {
            DispatcherQueue.TryEnqueue(ApplyLocalization);
        }
    }

    private void ApplyLocalization()
    {
        _localization.Apply(Root);
        SettingsMenuItem.Text = _localization.Get("Dock.Settings");
        Island.RefreshLocalizedContent();
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        if (args.Previous.General.PassiveModuleReturnSeconds !=
            args.Current.General.PassiveModuleReturnSeconds)
        {
            _viewModel.Island.UpdateTemporarySelectionDuration(
                TimeSpan.FromSeconds(args.Current.General.PassiveModuleReturnSeconds));
            RefreshModuleReturnTimer();
        }

        if (args.Previous.Appearance != args.Current.Appearance ||
            args.Previous.Fullscreen.NotificationSeconds != args.Current.Fullscreen.NotificationSeconds)
        {
            ApplyAppearance(args.Current);
        }

        if (args.Previous.General.VisibilityMode != args.Current.General.VisibilityMode ||
            args.Previous.General.Position != args.Current.General.Position ||
            args.Previous.Monitor != args.Current.Monitor ||
            args.Previous.Fullscreen != args.Current.Fullscreen ||
            args.Previous.Privacy != args.Current.Privacy ||
            args.Previous.Modules != args.Current.Modules)
        {
            ApplyEnvironment();
        }

        if (args.Previous.Media != args.Current.Media)
        {
            _mediaSelectionCancellation?.Cancel();
            _mediaSelectionCancellation?.Dispose();
            _mediaSelectionCancellation = new CancellationTokenSource();
            _ = ApplyMediaSelectionAsync(args.Current.Media, _mediaSelectionCancellation.Token);
        }
    }

    private void ApplyAppearance(MiaDockSettings settings)
    {
        var appearance = settings.Appearance;
        var motionOptions = SettingsMapper.ToMotionOptions(settings);
        var layoutOptions = SettingsMapper.ToLayoutOptions(appearance);
        var isFirstApplication = _appliedAppearance is null;
        var themeChanged = isFirstApplication || _appliedAppearance!.Theme != appearance.Theme;
        var paletteChanged = themeChanged ||
                             _appliedAppearance!.BackgroundColor != appearance.BackgroundColor ||
                             _appliedAppearance.AccentColor != appearance.AccentColor ||
                             _appliedAppearance.Opacity != appearance.Opacity;
        var layoutChanged = _appliedLayoutOptions != layoutOptions;
        var motionChanged = _appliedMotionOptions != motionOptions;

        // Reloading merged dictionaries, Acrylic and the animation coordinator on
        // every slider tick can block the UI thread. Apply only the layer affected
        // by the changed setting.
        if (paletteChanged)
        {
            _themeService.Apply(appearance);
        }

        if (themeChanged)
        {
            Island.Theme = appearance.Theme.ToString();
            SystemBackdrop = null;
            ApplyOverlayBackdrop(appearance.Theme);
            Root.RequestedTheme = appearance.Theme.UsesColorlessGlass() ||
                                  appearance.Theme is ThemeStyle.AppleLike or ThemeStyle.OledBlack
                ? ElementTheme.Dark
                : ElementTheme.Default;
            Root.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        }

        Island.ApplyAppearance(appearance);
        _windowController.UpdateSurfaceColor(ToLayeredSurfaceArgb(appearance));
        _windowController.UpdateOpacity(appearance.Opacity);

        if (motionChanged)
        {
            _autoCollapse.UpdateOptions(motionOptions);
        }

        if (layoutChanged || motionChanged)
        {
            Island.ConfigureAnimations(
                _animationPreferences,
                motionOptions,
                layoutOptions,
                _viewModel.Island.CurrentState);
        }

        if (layoutChanged)
        {
            _windowController.UpdateLayout(Island.VisualSize, Island.VisualCornerRadius);
        }

        Island.ShowNotificationControls = settings.Fullscreen.Style == FullscreenNotificationStyle.WithControls;
        _appliedAppearance = appearance;
        _appliedLayoutOptions = layoutOptions;
        _appliedMotionOptions = motionOptions;
    }

    private static uint ToLayeredSurfaceArgb(AppearanceSettings appearance)
    {
        if (appearance.Theme.UsesColorlessGlass())
        {
            // The layered helper only paints the anti-aliased outer feather.
            // A faint neutral stroke keeps that edge smooth without placing a
            // dark bitmap behind Acrylic, which would prevent live sampling.
            return 0x20FFFFFF;
        }

        var color = ColorParser.ParseRgb(appearance.BackgroundColor);
        var alpha = checked((byte)Math.Round(
            Math.Clamp(appearance.Opacity, 0.35, 1) * byte.MaxValue));
        return (uint)alpha << 24 |
               (uint)color.R << 16 |
               (uint)color.G << 8 |
               color.B;
    }

    private async Task ApplyMediaSelectionAsync(MediaSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            await _media.SetSelectionAsync(SettingsMapper.ToMediaSelection(settings), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _log.Write(
                TechnicalLogLevel.Error,
                TechnicalEventIds.MediaSelectionFailed,
                "Media",
                "Media source selection failed.",
                exception,
                new Dictionary<string, object?> { ["operation"] = "select-source" });
        }
    }

    private void ApplyTransparentWindowBackdrop()
    {
        if (_transparentWindowBackdrop is not null)
        {
            return;
        }

        DispatcherQueue.EnsureSystemDispatcherQueue();
        _windowBackdropTarget = this.As<ICompositionSupportsSystemBackdrop>();
        _transparentBackdropCompositor = new Windows.UI.Composition.Compositor();
        _transparentWindowBackdrop = _transparentBackdropCompositor.CreateColorBrush(
            Color.FromArgb(0, 0, 0, 0));
        _windowBackdropTarget.SystemBackdrop = _transparentWindowBackdrop;
    }

    private void ClearTransparentWindowBackdrop()
    {
        if (_windowBackdropTarget is not null)
        {
            _windowBackdropTarget.SystemBackdrop = null;
            _windowBackdropTarget = null;
        }

        _transparentWindowBackdrop?.Dispose();
        _transparentWindowBackdrop = null;
        _transparentBackdropCompositor?.Dispose();
        _transparentBackdropCompositor = null;
    }

    private void ApplyOverlayBackdrop(ThemeStyle theme)
    {
        ClearColorlessAcrylicBackdrop();
        Island.ClearSystemBackdrop();

        if (theme.UsesColorlessGlass())
        {
            if (TryApplyColorlessAcrylicBackdrop())
            {
                return;
            }

            // Keep the layered host transparent when Acrylic is unavailable.
            // The subtle surface overlay remains visible without a black rectangle.
            ApplyTransparentWindowBackdrop();
            return;
        }

        ApplyTransparentWindowBackdrop();
        Island.ApplySystemBackdrop(theme);
    }

    private bool TryApplyColorlessAcrylicBackdrop()
    {
        if (!DesktopAcrylicController.IsSupported())
        {
            return false;
        }

        DesktopAcrylicController? controller = null;
        try
        {
            ClearTransparentWindowBackdrop();
            DispatcherQueue.EnsureSystemDispatcherQueue();
            var configuration = new SystemBackdropConfiguration
            {
                // The dock intentionally does not activate on hover. Keeping
                // backdrop input active prevents Acrylic's opaque inactive
                // fallback from replacing the live desktop sample.
                IsInputActive = true,
                Theme = SystemBackdropTheme.Dark
            };
            controller = new DesktopAcrylicController
            {
                Kind = DesktopAcrylicKind.Thin,
                FallbackColor = Color.FromArgb(0, 0, 0, 0),
                TintColor = Color.FromArgb(255, 128, 128, 128),
                TintOpacity = 0.02f,
                LuminosityOpacity = 0.10f
            };
            controller.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
            controller.SetSystemBackdropConfiguration(configuration);
            _glassAcrylicController = controller;
            _glassBackdropConfiguration = configuration;
            return true;
        }
        catch (Exception exception)
        {
            controller?.Dispose();
            ApplyTransparentWindowBackdrop();
            _log.Write(
                TechnicalLogLevel.Warning,
                "overlay-glass-unavailable",
                "Overlay",
                "Colorless Acrylic could not be applied; using the system fallback.",
                exception);
            return false;
        }
    }

    private void ClearColorlessAcrylicBackdrop()
    {
        if (_glassAcrylicController is not null)
        {
            _glassAcrylicController.RemoveAllSystemBackdropTargets();
            _glassAcrylicController.Dispose();
            _glassAcrylicController = null;
        }

        _glassBackdropConfiguration = null;
    }

    private void OnThemeEnvironmentChanged(object? sender, EventArgs args)
    {
        void Refresh()
        {
            if (_settings.Current.Appearance.Theme != ThemeStyle.AdaptiveFluent)
            {
                return;
            }

            Root.RequestedTheme = ElementTheme.Default;
            Island.ApplyAppearance(_settings.Current.Appearance);
            _windowController.UpdateSurfaceColor(ToLayeredSurfaceArgb(_settings.Current.Appearance));
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            Refresh();
        }
        else
        {
            DispatcherQueue.TryEnqueue(Refresh);
        }
    }

    private void OnFullscreenStateChanged(object? sender, FullscreenSnapshot snapshot)
    {
        var wasFullscreen = _fullscreenState.IsFullscreen;
        _fullscreenState = snapshot;
        if (wasFullscreen && !snapshot.IsFullscreen && _temporaryNotificationVisible)
        {
            _temporaryNotificationVisible = false;
            _viewModel.Island.HandleNotificationElapsed();
        }

        ApplyEnvironment();
    }

    private void OnDisplaysChanged(object? sender, IReadOnlyList<DisplayDescriptor> displays) => ApplyEnvironment();

    private void OnSessionLockStateChanged(object? sender, bool isLocked)
    {
        if (isLocked &&
            !_privacyPolicy.CanPresent(
                _viewModel.Island.ActiveModulePresentation,
                _settings.Current,
                _fullscreenState.IsFullscreen,
                true))
        {
            _temporaryNotificationVisible = false;
        }
        ApplyEnvironment();
    }

    private void OnSystemResumed(object? sender, EventArgs args)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _viewModel.Island.NotifyModuleInteraction();
            RefreshModuleReturnTimer();
            ApplyEnvironment();
        });
    }

    private void OnModuleEventOccurred(object? sender, ModuleEvent moduleEvent)
    {
        if (!_focusPolicy.Current.AllowsEvent(moduleEvent) ||
            moduleEvent.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return;
        }

        var settings = _settings.Current;
        var moduleAllowsFullscreen = !settings.Modules.TryGetValue(moduleEvent.ModuleId, out var moduleSettings) ||
                                     moduleSettings.ShowInFullscreen;
        var fullscreenAllowsEvent = settings.Tray.EnableTemporaryNotifications &&
            (!_fullscreenState.IsFullscreen
            || (settings.Fullscreen.Enabled &&
                _focusPolicy.Current.AllowFullscreenNotifications &&
                moduleAllowsFullscreen &&
                moduleEvent.IsFullscreenEligible &&
                (moduleEvent.Kind != ModuleEventKind.TrackChanged || settings.Fullscreen.ShowTrackChanges))) &&
            _privacyPolicy.CanPresent(
                moduleEvent.Presentation,
                settings,
                _fullscreenState.IsFullscreen,
                _sessionLockState.IsLocked);
        _temporaryNotificationVisible = fullscreenAllowsEvent;
        ApplyEnvironment();
    }

    private void ApplyEnvironment()
    {
        var settings = _settings.Current;
        var focus = _focusPolicy.Current;
        var display = _fullscreenState.IsFullscreen && _fullscreenState.WindowHandle != 0
            ? _displayTopology.ResolveForWindow(_fullscreenState.WindowHandle)
            : _displayTopology.Resolve(settings.Monitor, _fullscreenState.WindowHandle);
        _windowController.UpdatePlacement(
            SettingsMapper.ToOverlayPosition(settings.General.Position),
            display.Id);

        var normalVisibilityAllowsDock = focus.AllowsNormalDock(
            settings.General.VisibilityMode == IslandVisibilityMode.Always);
        var temporaryVisibilityAllowsDock =
            _temporaryNotificationVisible &&
            focus.AllowsTemporaryDock(_fullscreenState.IsFullscreen);
        var shouldShow = _fullscreenState.IsFullscreen
            ? temporaryVisibilityAllowsDock &&
              settings.Fullscreen.Enabled &&
              focus.AllowFullscreenNotifications
            : (!_manuallyHidden && normalVisibilityAllowsDock) ||
              temporaryVisibilityAllowsDock;
        shouldShow &= _privacyPolicy.CanPresent(
            _viewModel.Island.ActiveModulePresentation,
            settings,
            _fullscreenState.IsFullscreen,
            _sessionLockState.IsLocked);
        if (shouldShow)
        {
            _windowController.ShowNoActivate();
        }
        else
        {
            _windowController.Hide();
        }
    }

    private void OnFocusPolicyChanged(object? sender, EventArgs args)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            ApplyFocusPolicyChange();
        }
        else
        {
            DispatcherQueue.TryEnqueue(ApplyFocusPolicyChange);
        }
    }

    private void ApplyFocusPolicyChange()
    {
        var activeEvent = _viewModel.Island.LastModuleEvent;
        var notificationIsNoLongerAllowed =
            activeEvent is null ||
            !_focusPolicy.Current.AllowsEvent(activeEvent) ||
            (_fullscreenState.IsFullscreen &&
             !_focusPolicy.Current.AllowFullscreenNotifications);
        if (_temporaryNotificationVisible && notificationIsNoLongerAllowed)
        {
            _temporaryNotificationVisible = false;
            if (_viewModel.Island.CurrentState == IslandVisualState.ModuleNotification &&
                activeEvent is null)
            {
                _viewModel.Island.HandleNotificationElapsed();
            }
        }

        ApplyEnvironment();
    }
}
