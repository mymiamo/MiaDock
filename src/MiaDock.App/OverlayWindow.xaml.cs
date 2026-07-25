using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Dispatching;
using MiaDock.App.Services;
using MiaDock.App.ViewModels;
using MiaDock.Core.Presentation;
using MiaDock.Core.Settings;
using MiaDock.Modules.Media.Services;
using MiaDock.Platform.Windows.Overlay;
using MiaDock.UI.Services;
using Microsoft.UI.Xaml.Media;
using MiaDock.Core.Modules;
using MiaDock.Platform.Windows.Display;
using MiaDock.Platform.Windows.Fullscreen;
using MiaDock.Core.Logging;
using MiaDock.App.Infrastructure;
using MiaDock.Platform.Windows.Lifecycle;
using MiaDock.Core.Theming;
using MiaDock.Modules.Time.Services;
using Microsoft.UI.Composition.SystemBackdrops;
using Windows.UI;

namespace MiaDock.App;

public sealed partial class OverlayWindow : Window
{
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
    private readonly ILogService _log;
    private CancellationTokenSource? _mediaSelectionCancellation;
    private FullscreenSnapshot _fullscreenState = FullscreenSnapshot.None;
    private bool _temporaryNotificationVisible;
    private bool _manuallyHidden = true;

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
        PresentationPrivacyPolicy privacyPolicy)
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
        Root.DataContext = viewModel;
        Island.ConfigureModuleViews(moduleViews);

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
        _viewModel.Island.SelectNextModule();
        RegisterDockActivity();
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

        if (delta > 0)
        {
            _viewModel.Island.SelectPreviousModule();
        }
        else
        {
            _viewModel.Island.SelectNextModule();
        }

        args.Handled = true;
        RegisterDockActivity();
    }

    private void OnIslandTapped(object sender, TappedRoutedEventArgs args)
    {
        if (_viewModel.Island.CurrentState != IslandVisualState.ExpandedModule &&
            _settings.Current.General.InteractionMode is IslandInteractionMode.Click or IslandInteractionMode.HoverAndClick)
        {
            _viewModel.Island.HandlePrimaryInvoked();
        }

        RegisterDockActivity();
    }

    private void OnOutsidePointerPressed(object? sender, EventArgs args)
    {
        if (_viewModel.Island.CurrentState != IslandVisualState.ExpandedModule)
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

    private void OnSettingsClick(object sender, RoutedEventArgs args) => _settingsWindow.Show();

    private void OnPreviousModuleRequested(object? sender, EventArgs args)
    {
        _viewModel.Island.SelectPreviousModule();
        RegisterDockActivity();
    }

    private void OnNextModuleRequested(object? sender, EventArgs args)
    {
        _viewModel.Island.SelectNextModule();
        RegisterDockActivity();
    }

    private void OnModuleSelected(object? sender, Controls.ModuleSelectedEventArgs args)
    {
        _viewModel.Island.SelectModule(args.ModuleId);
        RegisterDockActivity();
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
        _windowController.SetOutsideClickMonitoring(
            transition.CurrentState == IslandVisualState.ExpandedModule);

        if (transition.CurrentState == IslandVisualState.ModuleNotification)
        {
            _autoCollapse.SetNotificationDuration(_viewModel.Island.ActiveEventDisplayDuration);
        }

        Island.ApplyTransition(transition);
        _autoCollapse.ObserveTransition(transition);
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
        _windowController.OutsidePointerPressed -= OnOutsidePointerPressed;
        _fullscreen.Dispose();
        _mediaSelectionCancellation?.Cancel();
        _mediaSelectionCancellation?.Dispose();
        Closed -= OnClosed;
        _windowController.Dispose();
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
        _themeService.Apply(settings.Appearance);
        Island.Theme = settings.Appearance.Theme.ToString();
        Island.ApplyAppearance(settings.Appearance);

        ApplySystemBackdrop(settings.Appearance.Theme);
        Root.RequestedTheme = settings.Appearance.Theme == ThemeStyle.BlurredGlass
            ? ElementTheme.Dark
            : ElementTheme.Default;
        Root.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
        _windowController.UpdateOpacity(settings.Appearance.Opacity);

        var motionOptions = SettingsMapper.ToMotionOptions(settings);
        var layoutOptions = SettingsMapper.ToLayoutOptions(settings.Appearance);
        _autoCollapse?.UpdateOptions(motionOptions);
        Island.ConfigureAnimations(
            _animationPreferences,
            motionOptions,
            layoutOptions,
            _viewModel.Island.CurrentState);
        _windowController.UpdateLayout(Island.VisualSize, Island.VisualCornerRadius);
        Island.ShowNotificationControls = settings.Fullscreen.Style == FullscreenNotificationStyle.WithControls;
    }

    private void ApplySystemBackdrop(ThemeStyle theme)
    {
        SystemBackdrop = theme switch
        {
            ThemeStyle.Windows11Mica => new MicaBackdrop { Kind = MicaKind.Base },
            ThemeStyle.Windows11MicaAlt => new MicaBackdrop { Kind = MicaKind.BaseAlt },
            ThemeStyle.Windows11Acrylic or
            ThemeStyle.Windows11AcrylicThin or
            ThemeStyle.BlurredGlass =>
                new DesktopAcrylicBackdrop(),
            _ => null
        };
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
        var settings = _settings.Current;
        var moduleAllowsFullscreen = !settings.Modules.TryGetValue(moduleEvent.ModuleId, out var moduleSettings) ||
                                     moduleSettings.ShowInFullscreen;
        var fullscreenAllowsEvent = _settings.Current.Tray.EnableTemporaryNotifications &&
            (!_fullscreenState.IsFullscreen
            || (_settings.Current.Fullscreen.Enabled &&
                moduleAllowsFullscreen &&
                moduleEvent.IsFullscreenEligible &&
                (moduleEvent.Kind != ModuleEventKind.TrackChanged || _settings.Current.Fullscreen.ShowTrackChanges))) &&
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
        var display = _fullscreenState.IsFullscreen && _fullscreenState.WindowHandle != 0
            ? _displayTopology.ResolveForWindow(_fullscreenState.WindowHandle)
            : _displayTopology.Resolve(settings.Monitor, _fullscreenState.WindowHandle);
        _windowController.UpdatePlacement(
            SettingsMapper.ToOverlayPosition(settings.General.Position),
            display.Id);

        var shouldShow = _fullscreenState.IsFullscreen
            ? _temporaryNotificationVisible && settings.Fullscreen.Enabled
            : (!_manuallyHidden && settings.General.VisibilityMode == IslandVisibilityMode.Always) ||
              _temporaryNotificationVisible;
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
}
