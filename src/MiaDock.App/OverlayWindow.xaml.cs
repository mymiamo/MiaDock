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
using MiaDock.Core.Overlay;
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
using MiaDock.Core.Audio;
using Windows.UI;
using WinRT;

namespace MiaDock.App;

public sealed partial class OverlayWindow : Window
{
    private const long WheelNavigationThrottleMilliseconds = 90;
    private const long PointerActivityThrottleMilliseconds = 200;
    private const double EdgeRevealStripThicknessInDips = 15;
    private readonly OverlayWindowViewModel _viewModel;
    private readonly IOverlayWindowController _windowController;
    private readonly IslandAutoCollapseController _autoCollapse;
    private readonly DispatcherQueueTimer _moduleReturnTimer;
    private readonly DispatcherQueueTimer _edgeRevealPollTimer;
    private readonly DispatcherQueueTimer _edgeRevealHideTimer;
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
    private readonly OverlayWindowHandleProvider _windowHandleProvider;
    private readonly IAudibleNotificationPlayer _audibleNotificationPlayer;
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
    private bool _isPointerOverDock;
    private bool _isPointerPressed;
    private bool _edgeRevealHoverVisible;
    private bool _fullscreenAffectsDockDisplay;
    private long _lastWheelNavigationTimestamp;
    private long _lastPointerActivityTimestamp;

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
        IAppLocalizationService localization,
        OverlayWindowHandleProvider windowHandleProvider,
        IAudibleNotificationPlayer audibleNotificationPlayer)
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
        _windowHandleProvider = windowHandleProvider;
        _audibleNotificationPlayer = audibleNotificationPlayer;
        Root.DataContext = viewModel;
        ApplyTransparentWindowBackdrop();
        Island.ConfigureModuleViews(moduleViews);
        Island.ConfigureLocalization(localization);
        ApplyLocalization();

        var initialAppearance = settings.Current.Appearance;
        _windowController = controllerFactory.Create(
            this,
            new OverlayWindowOptions(
                SettingsMapper.ToOverlayPosition(settings.Current.General.Position),
                new OverlaySize(initialAppearance.CollapsedWidth, initialAppearance.CollapsedHeight),
                initialAppearance.EdgeMargin,
                initialAppearance.EffectiveCornerRadii));
        _windowHandleProvider.SetWindowHandle(_windowController.WindowHandle);
        _windowController.OutsidePointerPressed += OnOutsidePointerPressed;
        var motionOptions = SettingsMapper.ToMotionOptions(settings.Current);
        _autoCollapse = new IslandAutoCollapseController(DispatcherQueue, motionOptions);
        _autoCollapse.Elapsed += OnAutoCollapseElapsed;
        _moduleReturnTimer = DispatcherQueue.CreateTimer();
        _moduleReturnTimer.IsRepeating = false;
        _moduleReturnTimer.Tick += OnModuleReturnTimerTick;
        _edgeRevealPollTimer = DispatcherQueue.CreateTimer();
        _edgeRevealPollTimer.Interval = TimeSpan.FromMilliseconds(200);
        _edgeRevealPollTimer.IsRepeating = true;
        _edgeRevealPollTimer.Tick += OnEdgeRevealPollTimerTick;
        _edgeRevealHideTimer = DispatcherQueue.CreateTimer();
        _edgeRevealHideTimer.Interval = TimeSpan.FromMilliseconds(450);
        _edgeRevealHideTimer.IsRepeating = false;
        _edgeRevealHideTimer.Tick += OnEdgeRevealHideTimerTick;
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
        _animationPreferences.AnimationsEnabledChanged += OnAnimationsEnabledChanged;
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
        _windowController.UpdateLayout(Island.VisualSize, Island.VisualCornerRadii);
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
        _isPointerOverDock = true;
        _edgeRevealHoverVisible = true;
        _edgeRevealHideTimer.Stop();
        _autoCollapse.PointerEntered();
        if (_settings.Current.General.InteractionMode is IslandInteractionMode.Hover or IslandInteractionMode.HoverAndClick)
        {
            _viewModel.Island.HandlePointerEntered();
        }

        RegisterDockActivity();
        _lastPointerActivityTimestamp = Environment.TickCount64;
        ApplyEnvironment();
    }

    private void OnIslandPointerExited(object sender, PointerRoutedEventArgs args)
    {
        _isPointerOverDock = false;
        _autoCollapse.PointerExited();
        ScheduleEdgeRevealHide();
    }

    private void OnIslandPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        var now = Environment.TickCount64;
        if (_lastPointerActivityTimestamp != 0 &&
            now - _lastPointerActivityTimestamp < PointerActivityThrottleMilliseconds)
        {
            return;
        }

        _lastPointerActivityTimestamp = now;
        RegisterDockActivity();
    }

    private void OnIslandPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        _isPointerPressed = true;
        _edgeRevealHideTimer.Stop();
        ApplyEnvironment();
    }

    private void OnIslandPointerReleased(object sender, PointerRoutedEventArgs args) =>
        EndPointerPress();

    private void OnIslandPointerCanceled(object sender, PointerRoutedEventArgs args) =>
        EndPointerPress();

    private void OnIslandPointerCaptureLost(object sender, PointerRoutedEventArgs args) =>
        EndPointerPress();

    private void EndPointerPress()
    {
        _isPointerPressed = false;
        ScheduleEdgeRevealHide();
        ApplyEnvironment();
    }

    private void OnIslandRightTapped(object sender, RightTappedRoutedEventArgs args)
    {
        args.Handled = true;
        EndPointerPress();
    }

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
        if (args.PropertyName == nameof(_viewModel.Island.LastModuleEvent))
        {
            TryPlayActiveAudibleEvent();
            return;
        }

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
        _windowController.UpdateLayout(Island.VisualSize, Island.VisualCornerRadii);

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
        ApplyEnvironment();
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
            _edgeRevealHideTimer.Stop();
            ApplyEnvironment();
            return;
        }

        var state = _viewModel.Island.CurrentState;
        _isPointerOverDock = _windowController.IsPointerOverWindow();
        _autoCollapse.ResumeTransientInteraction(state, _isPointerOverDock);
        _windowController.SetOutsideClickMonitoring(
            state == IslandVisualState.ExpandedModule);
        ScheduleEdgeRevealHide();
        ApplyEnvironment();
    }

    private void OnAutoCollapseElapsed(object? sender, IslandTrigger trigger)
    {
        if (DockInteractionSession.IsActive || _isPointerPressed)
        {
            return;
        }

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
        // Closed is raised while Windows is still pumping WM_DESTROY on a native
        // stack. An escaping managed exception there fails fast as a bogus
        // "stack buffer overrun", so every teardown step is isolated.
        try
        {
            _autoCollapse.Elapsed -= OnAutoCollapseElapsed;
            _autoCollapse.Dispose();
            _moduleReturnTimer.Stop();
            _moduleReturnTimer.Tick -= OnModuleReturnTimerTick;
            _edgeRevealPollTimer.Stop();
            _edgeRevealPollTimer.Tick -= OnEdgeRevealPollTimerTick;
            _edgeRevealHideTimer.Stop();
            _edgeRevealHideTimer.Tick -= OnEdgeRevealHideTimerTick;
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
            _animationPreferences.AnimationsEnabledChanged -= OnAnimationsEnabledChanged;
            DockInteractionSession.ActivityChanged -= OnDockInteractionActivityChanged;
            _windowController.OutsidePointerPressed -= OnOutsidePointerPressed;
            _fullscreen.Dispose();
            _mediaSelectionCancellation?.Cancel();
            _mediaSelectionCancellation?.Dispose();
            try
            {
                Island.ClearSystemBackdrop();
            }
            catch (Exception)
            {
            }

            ClearTransparentWindowBackdrop();
            Closed -= OnClosed;
            _windowController.Dispose();
            _windowHandleProvider.SetWindowHandle(0);
        }
        catch (Exception exception)
        {
            _log.Write(
                TechnicalLogLevel.Warning,
                TechnicalEventIds.ApplicationShutdownFailed,
                "Overlay",
                "Overlay teardown encountered a recoverable error.",
                exception);
        }
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
        Island.RefreshLocalizedContent();
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        if (args.Previous.AudibleNotifications != args.Current.AudibleNotifications &&
            (_viewModel.Island.LastModuleEvent is not { } activeEvent ||
             !args.Current.AudibleNotifications.Allows(activeEvent.AudibleCue)))
        {
            _audibleNotificationPlayer.Stop();
        }

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
            args.Previous.Appearance.EdgeMargin != args.Current.Appearance.EdgeMargin ||
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
            // Window.SystemBackdrop owns the same composition slot as the manual
            // transparency brush, so assigning it here would make the HWND opaque
            // and paint a black rectangle around the dock.
            ApplyOverlayBackdrop(appearance.Theme);
            Root.RequestedTheme = appearance.Theme.UsesColorlessGlass() ||
                                  appearance.Theme is ThemeStyle.AppleLike or ThemeStyle.OledBlack
                                      or ThemeStyle.TozPembe
                ? ElementTheme.Dark
                : ElementTheme.Default;
            Root.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        }

        Island.ApplyAppearance(appearance);
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

        // Always push the live silhouette after appearance/layout edits so radius
        // changes are visible even when only CornerRadii differ.
        _windowController.UpdateLayout(Island.VisualSize, Island.VisualCornerRadii);

        Island.ShowNotificationControls = settings.Fullscreen.Style == FullscreenNotificationStyle.WithControls;
        _appliedAppearance = appearance;
        _appliedLayoutOptions = layoutOptions;
        _appliedMotionOptions = motionOptions;
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
        // The brush comes from a separate system Compositor, so it is attached
        // exactly once. Re-assigning it on a composed window corrupts the native
        // backdrop state instead of refreshing it.
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
        // Detaching and disposing the manual transparent brush races with DWM
        // and WinUI tearing down the same composition slot. Swallow failures so
        // the HWND can finish destroying without a reverse-P/Invoke fail-fast.
        try
        {
            if (_windowBackdropTarget is not null)
            {
                _windowBackdropTarget.SystemBackdrop = null;
            }
        }
        catch (Exception)
        {
        }
        finally
        {
            _windowBackdropTarget = null;
        }

        try
        {
            _transparentWindowBackdrop?.Dispose();
        }
        catch (Exception)
        {
        }
        finally
        {
            _transparentWindowBackdrop = null;
        }

        try
        {
            _transparentBackdropCompositor?.Dispose();
        }
        catch (Exception)
        {
        }
        finally
        {
            _transparentBackdropCompositor = null;
        }
    }

    private void ApplyOverlayBackdrop(ThemeStyle theme)
    {
        Island.ClearSystemBackdrop();

        // Every material is hosted on the backdrop element, never on the window.
        // A window level backdrop would paint the whole HWND rectangle and could
        // only be rounded by a 1-bit GDI region, which destroys the edge
        // anti-aliasing of the configured corner radius.
        ApplyTransparentWindowBackdrop();
        Island.ApplySystemBackdrop(theme);
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
        var fullscreenTargetChanged =
            _fullscreenState.WindowHandle != snapshot.WindowHandle ||
            _fullscreenState.MonitorHandle != snapshot.MonitorHandle;
        _fullscreenState = snapshot;
        if (wasFullscreen && !snapshot.IsFullscreen && _temporaryNotificationVisible)
        {
            _temporaryNotificationVisible = false;
            _viewModel.Island.HandleNotificationElapsed();
        }

        if (!snapshot.IsFullscreen || fullscreenTargetChanged)
        {
            _edgeRevealHoverVisible = false;
            _edgeRevealHideTimer.Stop();
        }

        ApplyEnvironment();
    }

    private void OnEdgeRevealPollTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (!IsEdgeRevealModeActive())
        {
            sender.Stop();
            return;
        }

        if (!_edgeRevealHoverVisible && _windowController.IsPointerAtAttachedEdge())
        {
            _edgeRevealHoverVisible = true;
            _edgeRevealHideTimer.Stop();
            ApplyEnvironment();
        }
    }

    private void OnEdgeRevealHideTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        if (!IsEdgeRevealModeActive() ||
            _isPointerOverDock ||
            _isPointerPressed ||
            DockInteractionSession.IsActive ||
            _viewModel.Island.CurrentState == IslandVisualState.ExpandedModule ||
            _temporaryNotificationVisible)
        {
            return;
        }

        _edgeRevealHoverVisible = false;
        ApplyEnvironment();
    }

    private void ScheduleEdgeRevealHide()
    {
        if (!IsEdgeRevealModeActive() ||
            _isPointerOverDock ||
            _isPointerPressed ||
            DockInteractionSession.IsActive ||
            _viewModel.Island.CurrentState == IslandVisualState.ExpandedModule ||
            _temporaryNotificationVisible)
        {
            return;
        }

        _edgeRevealHideTimer.Stop();
        _edgeRevealHideTimer.Start();
    }

    private void OnDisplaysChanged(object? sender, IReadOnlyList<DisplayDescriptor> displays) => ApplyEnvironment();

    private void OnSessionLockStateChanged(object? sender, bool isLocked)
    {
        var fullscreenAffectsDock = DoesFullscreenAffectDisplay(
            ResolveDockDisplay(_settings.Current));
        if (isLocked &&
            !_privacyPolicy.CanPresent(
                _viewModel.Island.ActiveModulePresentation,
                _settings.Current,
                fullscreenAffectsDock,
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
        if (!_focusPolicy.Current.AllowsEvent(moduleEvent) || moduleEvent.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return;
        }

        _temporaryNotificationVisible = CanPresentModuleEvent(moduleEvent);
        ApplyEnvironment();
    }

    private bool CanPresentModuleEvent(ModuleEvent moduleEvent)
    {
        if (!_focusPolicy.Current.AllowsEvent(moduleEvent) || moduleEvent.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return false;
        }

        var settings = _settings.Current;
        var dockDisplay = ResolveDockDisplay(settings);
        var fullscreenAffectsDock = DoesFullscreenAffectDisplay(dockDisplay);
        var moduleAllowsFullscreen = !settings.Modules.TryGetValue(moduleEvent.ModuleId, out var moduleSettings) ||
                                     moduleSettings.ShowInFullscreen;
        return settings.Tray.EnableTemporaryNotifications &&
            (!fullscreenAffectsDock
            || (settings.Fullscreen.Behavior != FullscreenDockBehavior.HideCompletely &&
                _focusPolicy.Current.AllowFullscreenNotifications &&
                moduleAllowsFullscreen &&
                moduleEvent.IsFullscreenEligible &&
                (moduleEvent.Kind != ModuleEventKind.TrackChanged || settings.Fullscreen.ShowTrackChanges))) &&
            _privacyPolicy.CanPresent(
                moduleEvent.Presentation,
                settings,
                fullscreenAffectsDock,
                _sessionLockState.IsLocked);
    }

    private void TryPlayActiveAudibleEvent()
    {
        if (_viewModel.Island.LastModuleEvent is not { } moduleEvent ||
            moduleEvent.AudibleCue == AudibleNotificationCue.None ||
            !_settings.Current.AudibleNotifications.Allows(moduleEvent.AudibleCue) ||
            !CanPresentModuleEvent(moduleEvent))
        {
            return;
        }

        _audibleNotificationPlayer.Play(moduleEvent.AudibleCue);
    }

    private void ApplyEnvironment()
    {
        var settings = _settings.Current;
        var focus = _focusPolicy.Current;
        var display = ResolveDockDisplay(settings);
        _fullscreenAffectsDockDisplay = DoesFullscreenAffectDisplay(display);
        _windowController.UpdatePlacement(
            SettingsMapper.ToOverlayPosition(settings.General.Position),
            display.Id,
            settings.Appearance.EdgeMargin);

        var normalVisibilityAllowsDock = focus.AllowsNormalDock(
            settings.General.VisibilityMode != IslandVisibilityMode.EventsOnly);
        var temporaryVisibilityAllowsDock =
            _temporaryNotificationVisible &&
            focus.AllowsTemporaryDock(_fullscreenAffectsDockDisplay);
        var decision = FullscreenDockVisibilityPolicy.Evaluate(
            new FullscreenDockVisibilityContext(
                settings.Fullscreen.Behavior,
                _fullscreenAffectsDockDisplay,
                _manuallyHidden,
                normalVisibilityAllowsDock,
                _temporaryNotificationVisible,
                temporaryVisibilityAllowsDock && focus.AllowFullscreenNotifications,
                _edgeRevealHoverVisible,
                DockInteractionSession.IsActive,
                _isPointerPressed,
                _viewModel.Island.CurrentState == IslandVisualState.ExpandedModule));
        if (!_fullscreenAffectsDockDisplay &&
            settings.General.VisibilityMode == IslandVisibilityMode.EdgeReveal &&
            !_manuallyHidden &&
            normalVisibilityAllowsDock)
        {
            var revealShelf = _temporaryNotificationVisible ||
                              _edgeRevealHoverVisible ||
                              DockInteractionSession.IsActive ||
                              _isPointerPressed ||
                              _viewModel.Island.CurrentState == IslandVisualState.ExpandedModule;
            decision = new FullscreenDockVisibilityDecision(
                ShowWindow: true,
                HideAtEdge: !revealShelf,
                FullscreenPolicyApplied: false);
        }

        var shouldShow = decision.ShowWindow;
        shouldShow &= _privacyPolicy.CanPresent(
            _viewModel.Island.ActiveModulePresentation,
            settings,
            _fullscreenAffectsDockDisplay,
            _sessionLockState.IsLocked);
        UpdateEdgeRevealMonitoring(decision);
        if (shouldShow)
        {
            _windowController.ShowNoActivate();
        }
        else
        {
            _windowController.Hide();
        }
    }

    private DisplayDescriptor ResolveDockDisplay(MiaDockSettings settings) =>
        _displayTopology.Resolve(settings.Monitor, _fullscreenState.WindowHandle);

    private bool DoesFullscreenAffectDisplay(DisplayDescriptor dockDisplay)
    {
        if (!_fullscreenState.IsFullscreen || _fullscreenState.WindowHandle == 0)
        {
            return false;
        }

        var fullscreenDisplay = _displayTopology.ResolveForWindow(_fullscreenState.WindowHandle);
        return string.Equals(fullscreenDisplay.Id, dockDisplay.Id, StringComparison.Ordinal);
    }

    private void UpdateEdgeRevealMonitoring(FullscreenDockVisibilityDecision decision)
    {
        var shouldPoll = decision.ShowWindow && IsEdgeRevealModeActive();
        if (shouldPoll)
        {
            if (!_edgeRevealPollTimer.IsRunning)
            {
                _edgeRevealPollTimer.Start();
            }
            var position = SettingsMapper.ToOverlayPosition(_settings.Current.General.Position);
            var visibleStrip = EdgeRevealVisibleStripInDips(position);
            if (decision.HideAtEdge)
            {
                _windowController.SetEdgeRevealHidden(
                    hidden: true,
                    visibleStripInDips: visibleStrip,
                    animate: _animationPreferences.AnimationsEnabled,
                    transitionCompleted: () => Island.SetEdgeRevealAppearance(true, position));
            }
            else
            {
                Island.SetEdgeRevealAppearance(false, position);
                _windowController.SetEdgeRevealHidden(
                    hidden: false,
                    visibleStripInDips: visibleStrip,
                    animate: _animationPreferences.AnimationsEnabled);
            }
            return;
        }

        _edgeRevealPollTimer.Stop();
        _edgeRevealHideTimer.Stop();
        _edgeRevealHoverVisible = false;
        var restorePosition = SettingsMapper.ToOverlayPosition(_settings.Current.General.Position);
        Island.SetEdgeRevealAppearance(false, restorePosition);
        _windowController.SetEdgeRevealHidden(
            hidden: false,
            visibleStripInDips: EdgeRevealVisibleStripInDips(restorePosition),
            animate: _animationPreferences.AnimationsEnabled);
    }

    private static double EdgeRevealVisibleStripInDips(OverlayPosition position) =>
        EdgeRevealStripThicknessInDips;

    private bool IsEdgeRevealModeActive()
    {
        var settings = _settings.Current;
        return _fullscreenAffectsDockDisplay
            ? settings.Fullscreen.Behavior == FullscreenDockBehavior.EdgeReveal
            : settings.General.VisibilityMode == IslandVisibilityMode.EdgeReveal;
    }

    private void OnAnimationsEnabledChanged(object? sender, EventArgs args)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            ApplyEnvironment();
        }
        else
        {
            DispatcherQueue.TryEnqueue(ApplyEnvironment);
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
            (_fullscreenAffectsDockDisplay &&
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
