using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using MiaDock.App.Bootstrapper;
using MiaDock.App.Services;
using MiaDock.Modules.Media.Services;
using MiaDock.UI.Services;
using MiaDock.Core.Modules;
using MiaDock.Platform.Windows.Display;
using MiaDock.Core.Settings;
using MiaDock.Platform.Windows.Lifecycle;
using MiaDock.Core.Threading;
using MiaDock.Core.Logging;
using MiaDock.Modules.Time.Services;
using MiaDock.Modules.Notifications.Services;
using MiaDock.Core.Focus;
using Microsoft.Windows.AppLifecycle;

namespace MiaDock.App;

public partial class App : Application
{
    private readonly ServiceProvider _services;
    private OverlayWindow? _overlayWindow;
    private ISettingsWindowService? _settingsWindow;
    private IOnboardingWindowService? _onboardingWindow;
    private IApplicationLifetimeService? _lifetime;
    private ISingleInstanceService? _singleInstance;
    private IUiDispatcher? _uiDispatcher;
    private readonly AppExceptionCoordinator _exceptionCoordinator;
    private readonly ILogService _log;
    private bool _shutdownStarted;

    public App()
    {
        InitializeComponent();
        _services = ServiceRegistration.BuildServiceProvider();
        _log = _services.GetRequiredService<ILogService>();
        _exceptionCoordinator = _services.GetRequiredService<AppExceptionCoordinator>();
        _exceptionCoordinator.Attach(this);
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            await LaunchAsync(args);
        }
        catch (Exception exception)
        {
            _log.Write(
                TechnicalLogLevel.Critical,
                TechnicalEventIds.ApplicationUnhandled,
                "Application",
                "Application startup failed.",
                exception,
                new Dictionary<string, object?> { ["operation"] = "startup" });
            await _log.FlushAsync();
            if (_lifetime is not null)
            {
                _lifetime.RequestExit();
            }
            else
            {
                _exceptionCoordinator.Dispose();
                await _services.DisposeAsync();
                Exit();
            }
        }
    }

    private async Task LaunchAsync(LaunchActivatedEventArgs args)
    {
        _log.Write(
            TechnicalLogLevel.Information,
            TechnicalEventIds.ApplicationStarting,
            "Application",
            "Application startup began.",
            properties: new Dictionary<string, object?>
            {
                ["activationKind"] = GetActivationKind()
            });
        _singleInstance = _services.GetRequiredService<ISingleInstanceService>();
        if (!await _singleInstance.RegisterOrRedirectAsync("MiaDock.Main"))
        {
            await _services.DisposeAsync();
            Exit();
            return;
        }

        _singleInstance.ActivationRedirected += OnActivationRedirected;
        _lifetime = _services.GetRequiredService<IApplicationLifetimeService>();
        _lifetime.ExitRequested += OnExitRequested;
        _uiDispatcher = _services.GetRequiredService<IUiDispatcher>();

        var settings = _services.GetRequiredService<ISettingsService>();
        await settings.InitializeAsync();
        await _services.GetRequiredService<StartupTaskCoordinator>().ReconcileAsync();
        _services.GetRequiredService<IFocusService>().Start();
        _services.GetRequiredService<IFocusAutomationService>().Start();
        _services.GetRequiredService<IAppLocalizationService>().SetLanguage(settings.Current.General.Language);
        _services.GetRequiredService<IThemeService>().Apply(settings.Current.Appearance);
        _services.GetRequiredService<IDisplayTopologyService>().Start();
        try
        {
            _services.GetRequiredService<IWindowsSessionLockStateService>().Start();
        }
        catch (Exception exception)
        {
            _log.Write(
                TechnicalLogLevel.Warning,
                TechnicalEventIds.ApplicationStarting,
                "Privacy",
                "Windows session lock notifications are unavailable.",
                exception);
        }

        _overlayWindow = _services.GetRequiredService<OverlayWindow>();
        _settingsWindow = _services.GetRequiredService<ISettingsWindowService>();
        _onboardingWindow = _services.GetRequiredService<IOnboardingWindowService>();

        var media = _services.GetRequiredService<IMediaSessionService>();
        await media.InitializeAsync();
        await media.SetSelectionAsync(SettingsMapper.ToMediaSelection(settings.Current.Media));
        await _services.GetRequiredService<ITimeToolsService>().InitializeAsync();
        await _services.GetRequiredService<ISystemNotificationService>().InitializeAsync();
        var moduleRegistry = _services.GetRequiredService<IIslandModuleRegistry>();
        foreach (var module in moduleRegistry.Modules)
        {
            if (settings.Current.Modules.TryGetValue(module.Descriptor.Id, out var moduleSettings))
            {
                module.IsEnabled = moduleSettings.IsEnabled;
            }
        }

        await moduleRegistry.InitializeAsync();
        _services.GetRequiredService<ModuleSettingsCoordinator>().Start();
        if (!settings.Current.Onboarding.IsCompleted &&
            !await _onboardingWindow.ShowAsync())
        {
            return;
        }

        await _services.GetRequiredService<TrayMenuCoordinator>().StartAsync();
        _services.GetRequiredService<GlobalHotKeyCoordinator>().Start();
        _services.GetRequiredService<IStoreUpdateCoordinator>().Start();

        var showSettingsFromCommandLine = Environment.GetCommandLineArgs()
            .Skip(1)
            .Concat(args.Arguments.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(argument => string.Equals(argument, "--settings", StringComparison.OrdinalIgnoreCase));

        switch (showSettingsFromCommandLine
                    ? StartupLaunchMode.Settings
                    : settings.Current.StartupShutdown.LaunchMode)
        {
            case StartupLaunchMode.Settings:
                _settingsWindow.Show();
                break;
            case StartupLaunchMode.SilentTray:
                _overlayWindow.HideDock();
                break;
            default:
                _overlayWindow.ShowNoActivate();
                break;
        }

        _log.Write(
            TechnicalLogLevel.Information,
            TechnicalEventIds.ApplicationReady,
            "Application",
            "Application startup completed.");
    }

    private void OnActivationRedirected(object? sender, EventArgs args)
    {
        _uiDispatcher?.TryEnqueue(() =>
        {
            if (_onboardingWindow?.IsVisible == true)
            {
                _onboardingWindow.Activate();
                return;
            }

            _settingsWindow?.Show();
        });
    }

    private async void OnExitRequested(object? sender, EventArgs args)
    {
        if (_shutdownStarted)
        {
            return;
        }

        _shutdownStarted = true;
        try
        {
            _log.Write(
                TechnicalLogLevel.Information,
                TechnicalEventIds.ApplicationStopping,
                "Application",
                "Application shutdown began.");
            await _services.GetRequiredService<ISettingsService>().FlushAsync();
            _onboardingWindow?.CloseForShutdown();
            _settingsWindow?.CloseForShutdown();
            _overlayWindow?.Close();

            if (_singleInstance is not null)
            {
                _singleInstance.ActivationRedirected -= OnActivationRedirected;
            }

            if (_lifetime is not null)
            {
                _lifetime.ExitRequested -= OnExitRequested;
            }

            await _log.FlushAsync();
            _exceptionCoordinator.Dispose();
            await _services.DisposeAsync();
        }
        finally
        {
            ExitOnUiThread();
        }
    }

    private void ExitOnUiThread()
    {
        if (_uiDispatcher is null || _uiDispatcher.HasThreadAccess)
        {
            Exit();
            return;
        }

        if (!_uiDispatcher.TryEnqueue(Exit))
        {
            Environment.Exit(0);
        }
    }

    private static string GetActivationKind()
    {
        try
        {
            return AppInstance.GetCurrent()
                .GetActivatedEventArgs()
                .Kind
                .ToString();
        }
        catch
        {
            return "Unavailable";
        }
    }
}
