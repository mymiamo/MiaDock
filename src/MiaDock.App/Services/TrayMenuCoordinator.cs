using MiaDock.Core.Modules;
using MiaDock.Core.Localization;
using MiaDock.Core.Settings;
using MiaDock.Core.Focus;
using MiaDock.Core.Threading;
using MiaDock.Modules.Media.Models;
using MiaDock.Modules.Media.Services;
using MiaDock.Platform.Windows.Display;
using MiaDock.Platform.Windows.Startup;
using MiaDock.Platform.Windows.Tray;
using MiaDock.Core.Logging;

namespace MiaDock.App.Services;

public sealed class TrayMenuCoordinator : IDisposable
{
    private const string MediaModuleId = "media";
    private const int ToggleDockCommand = 100;
    private const int PreviousCommand = 110;
    private const int PlayPauseCommand = 111;
    private const int NextCommand = 112;
    private const int SettingsCommand = 200;
    private const int StartupCommand = 300;
    private const int FullscreenEnabledCommand = 400;
    private const int FullscreenMinimalCommand = 401;
    private const int FullscreenControlledCommand = 402;
    private const int PrimaryMonitorCommand = 500;
    private const int ActiveMonitorCommand = 501;
    private const int NotificationsCommand = 600;
    private const int DeactivateFocusCommand = 700;
    private const int ExitCommand = 900;
    private const int SourceCommandBase = 2000;
    private const int DisplayCommandBase = 3000;

    private readonly ITrayIconService _tray;
    private readonly IOverlayWindowService _overlay;
    private readonly ISettingsWindowService _settingsWindow;
    private readonly ISettingsService _settings;
    private readonly IIslandModuleRegistry _modules;
    private readonly IMediaSessionService _media;
    private readonly IDisplayTopologyService _displays;
    private readonly IStartupTaskService _startup;
    private readonly IApplicationLifetimeService _lifetime;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogService _log;
    private readonly ILocalizationService _localization;
    private readonly IFocusService _focus;
    private readonly Dictionary<int, string> _sourceCommands = new();
    private readonly Dictionary<int, string> _displayCommands = new();
    private StartupTaskStatus _startupStatus = StartupTaskStatus.Unavailable;
    private bool _started;
    private bool _disposed;

    public TrayMenuCoordinator(
        ITrayIconService tray,
        IOverlayWindowService overlay,
        ISettingsWindowService settingsWindow,
        ISettingsService settings,
        IIslandModuleRegistry modules,
        IMediaSessionService media,
        IDisplayTopologyService displays,
        IStartupTaskService startup,
        IApplicationLifetimeService lifetime,
        IUiDispatcher dispatcher,
        ILogService log,
        ILocalizationService localization,
        IFocusService focus)
    {
        _tray = tray;
        _overlay = overlay;
        _settingsWindow = settingsWindow;
        _settings = settings;
        _modules = modules;
        _media = media;
        _displays = displays;
        _startup = startup;
        _lifetime = lifetime;
        _dispatcher = dispatcher;
        _log = log;
        _localization = localization;
        _focus = focus;
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return;
        }

        _tray.Initialize("MiaDock");
        _tray.CommandInvoked += OnCommandInvoked;
        _tray.PrimaryInvoked += OnPrimaryInvoked;
        _settings.SettingsChanged += OnSettingsChanged;
        _modules.ActivePresentationChanged += OnModulePresentationChanged;
        _media.SnapshotChanged += OnMediaSnapshotChanged;
        _media.SourcesChanged += OnMediaSourcesChanged;
        _displays.DisplaysChanged += OnDisplaysChanged;
        _focus.FocusChanged += OnFocusChanged;
        _startupStatus = await _startup.GetStatusAsync(cancellationToken);
        _tray.SetMenu(BuildMenu());
        UpdateTrayVisibility();
        _started = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Mark disposed before unsubscribing so late media/tray callbacks become
        // no-ops instead of rebuilding menus during HWND teardown.
        _disposed = true;
        _tray.CommandInvoked -= OnCommandInvoked;
        _tray.PrimaryInvoked -= OnPrimaryInvoked;
        _settings.SettingsChanged -= OnSettingsChanged;
        _modules.ActivePresentationChanged -= OnModulePresentationChanged;
        _media.SnapshotChanged -= OnMediaSnapshotChanged;
        _media.SourcesChanged -= OnMediaSourcesChanged;
        _displays.DisplaysChanged -= OnDisplaysChanged;
        _focus.FocusChanged -= OnFocusChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
        _tray.Dispose();
    }

    private IReadOnlyList<TrayMenuItem> BuildMenu()
    {
        var settings = _settings.Current;

        _sourceCommands.Clear();
        var sourceItems = new List<TrayMenuItem>();
        for (var index = 0; index < _media.Sources.Count; index++)
        {
            var source = _media.Sources[index];
            var command = SourceCommandBase + index;
            _sourceCommands[command] = source.Id;
            sourceItems.Add(new TrayMenuItem(
                command,
                source.DisplayName,
                IsChecked: source.Id == settings.Media.SelectedSourceId,
                IsRadio: true));
        }

        if (sourceItems.Count == 0)
        {
            sourceItems.Add(new TrayMenuItem(0, Text("Tray.MediaNotFound"), IsEnabled: false));
        }

        _displayCommands.Clear();
        var monitorItems = new List<TrayMenuItem>
        {
            new(
                PrimaryMonitorCommand,
                Text("Tray.PrimaryMonitor"),
                IsChecked: settings.Monitor.Mode == MonitorSelectionMode.Primary,
                IsRadio: true),
            new(
                ActiveMonitorCommand,
                Text("Tray.ActiveMonitor"),
                IsChecked: settings.Monitor.Mode == MonitorSelectionMode.ActiveWindow,
                IsRadio: true),
            TrayMenuItem.Separator
        };
        for (var index = 0; index < _displays.Displays.Count; index++)
        {
            var display = _displays.Displays[index];
            var command = DisplayCommandBase + index;
            _displayCommands[command] = display.Id;
            monitorItems.Add(new TrayMenuItem(
                command,
                display.DisplayName,
                IsChecked: settings.Monitor.Mode == MonitorSelectionMode.Fixed &&
                           settings.Monitor.FixedMonitorId == display.Id,
                IsRadio: true));
        }

        var items = new List<TrayMenuItem>();
        if (_focus.Current is { IsActive: true, ActiveProfile: { } activeProfile })
        {
            items.Add(new TrayMenuItem(
                DeactivateFocusCommand,
                Text("Tray.FocusTurnOff", FocusProfileName(activeProfile)),
                IconKey: TrayIconKey.Focus));
            items.Add(TrayMenuItem.Separator);
        }

        items.Add(new TrayMenuItem(
            ToggleDockCommand,
            _overlay.IsDockVisible ? Text("Dock.Hide") : Text("Dock.Show"),
            IconKey: TrayIconKey.Window));
        items.Add(new TrayMenuItem(SettingsCommand, Text("Dock.Settings"), IconKey: TrayIconKey.Settings));
        items.Add(TrayMenuItem.Separator);

        if (settings.Tray.ShowMediaControls)
        {
            var playing = _media.Current.PlaybackStatus == PlaybackStatus.Playing;
            items.Add(new TrayMenuItem(
                PreviousCommand,
                Text("Tray.Previous"),
                _modules.CanExecuteCommand(MediaModuleId, "previous"),
                IconKey: TrayIconKey.Previous));
            items.Add(new TrayMenuItem(
                PlayPauseCommand,
                PlaybackLabel(),
                _modules.CanExecuteCommand(MediaModuleId, "play-pause"),
                IconKey: playing ? TrayIconKey.Pause : TrayIconKey.Play));
            items.Add(new TrayMenuItem(
                NextCommand,
                Text("Tray.Next"),
                _modules.CanExecuteCommand(MediaModuleId, "next"),
                IconKey: TrayIconKey.Next));
        }

        items.Add(new TrayMenuItem(0, Text("Tray.DefaultMedia"), Children: sourceItems, IconKey: TrayIconKey.Music));
        items.Add(TrayMenuItem.Separator);

        items.Add(new TrayMenuItem(
            NotificationsCommand,
            Text("Tray.TemporaryNotifications"),
            IsChecked: settings.Tray.EnableTemporaryNotifications,
            IconKey: TrayIconKey.Notifications));
        items.Add(new TrayMenuItem(0, Text("Tray.SelectMonitor"), Children: monitorItems, IconKey: TrayIconKey.Monitor));
        items.Add(TrayMenuItem.Separator);
        items.Add(new TrayMenuItem(ExitCommand, Text("Tray.Exit"), IconKey: TrayIconKey.Exit));
        return items;
    }

    private string PlaybackLabel() => _media.Current.PlaybackStatus == PlaybackStatus.Playing
        ? Text("Common.Pause")
        : Text("Common.Play");

    private void RefreshMenu()
    {
        if (!_disposed)
        {
            _tray.SetMenu(BuildMenu());
        }
    }

    private void OnPrimaryInvoked(object? sender, EventArgs args)
    {
        if (!_dispatcher.TryEnqueue(ExecutePrimaryActionSafely))
        {
            LogDispatchFailure("primary-action");
        }
    }

    private void ExecutePrimaryActionSafely()
    {
        try
        {
            if (_settings.Current.Tray.PrimaryAction == TrayPrimaryAction.ToggleDock)
            {
                _overlay.ToggleDock();
            }
            else
            {
                _settingsWindow.Show();
            }

            RefreshMenu();
        }
        catch (Exception exception)
        {
            _log.Write(
                TechnicalLogLevel.Error,
                TechnicalEventIds.TrayCommandFailed,
                "Tray",
                "The configured tray primary action failed safely.",
                exception,
                new Dictionary<string, object?>
                {
                    ["operation"] = "primary-action",
                    ["action"] = _settings.Current.Tray.PrimaryAction.ToString()
                });
        }
    }

    private void OnCommandInvoked(object? sender, int command)
    {
        if (!_dispatcher.TryEnqueue(() => _ = ExecuteCommandSafelyAsync(command)))
        {
            LogDispatchFailure("command");
        }
    }

    private void LogDispatchFailure(string operation) =>
        _log.Write(
            TechnicalLogLevel.Warning,
            TechnicalEventIds.TrayCommandFailed,
            "Tray",
            "A tray command was ignored because the UI dispatcher was unavailable.",
            properties: new Dictionary<string, object?> { ["operation"] = operation });

    private async Task ExecuteCommandSafelyAsync(int command)
    {
        try
        {
            await ExecuteCommandAsync(command);
        }
        catch (Exception exception)
        {
            _log.Write(
                TechnicalLogLevel.Error,
                TechnicalEventIds.TrayCommandFailed,
                "Tray",
                "A tray command failed.",
                exception,
                new Dictionary<string, object?> { ["operation"] = "command" });
        }
    }

    private async Task ExecuteCommandAsync(int command)
    {
        if (_lifetime.IsShuttingDown)
        {
            return;
        }

        if (_sourceCommands.TryGetValue(command, out var sourceId))
        {
            _settings.Update(settings => settings with
            {
                Media = settings.Media with { SelectedSourceId = sourceId }
            });
            return;
        }

        if (_displayCommands.TryGetValue(command, out var displayId))
        {
            _settings.Update(settings => settings with
            {
                Monitor = new MonitorSettings(MonitorSelectionMode.Fixed, displayId)
            });
            return;
        }

        switch (command)
        {
            case ToggleDockCommand:
                _overlay.ToggleDock();
                break;
            case PreviousCommand:
                await ExecuteModuleCommandAsync("previous");
                break;
            case PlayPauseCommand:
                await ExecuteModuleCommandAsync("play-pause");
                break;
            case NextCommand:
                await ExecuteModuleCommandAsync("next");
                break;
            case SettingsCommand:
                _settingsWindow.Show();
                break;
            case StartupCommand:
                var enabled = _startupStatus is StartupTaskStatus.Enabled or StartupTaskStatus.EnabledByPolicy;
                _startupStatus = await _startup.SetEnabledAsync(!enabled);
                _settings.Update(settings => settings with
                {
                    StartupShutdown = settings.StartupShutdown with
                    {
                        StartWithWindows = _startupStatus is StartupTaskStatus.Enabled or StartupTaskStatus.EnabledByPolicy
                    }
                });
                break;
            case FullscreenEnabledCommand:
                _settings.Update(settings => settings with
                {
                    Fullscreen = settings.Fullscreen with
                    {
                        Behavior = settings.Fullscreen.Behavior == FullscreenDockBehavior.HideCompletely
                            ? FullscreenDockBehavior.NotificationsOnly
                            : FullscreenDockBehavior.HideCompletely,
                        Enabled = settings.Fullscreen.Behavior == FullscreenDockBehavior.HideCompletely
                    }
                });
                break;
            case FullscreenMinimalCommand:
                SetFullscreenStyle(FullscreenNotificationStyle.Minimal);
                break;
            case FullscreenControlledCommand:
                SetFullscreenStyle(FullscreenNotificationStyle.WithControls);
                break;
            case PrimaryMonitorCommand:
                SetMonitorMode(MonitorSelectionMode.Primary);
                break;
            case ActiveMonitorCommand:
                SetMonitorMode(MonitorSelectionMode.ActiveWindow);
                break;
            case NotificationsCommand:
                _settings.Update(settings => settings with
                {
                    Tray = settings.Tray with
                    {
                        EnableTemporaryNotifications = !settings.Tray.EnableTemporaryNotifications
                    }
                });
                break;
            case DeactivateFocusCommand:
                if (_focus.Deactivate())
                {
                    _overlay.ShowDock();
                }
                break;
            case ExitCommand:
                // Tray commands run inside the shell/tray WndProc dispatch path.
                // Always marshal Exit onto a fresh dispatcher frame first.
                if (!_dispatcher.TryEnqueue(_lifetime.RequestExit))
                {
                    _lifetime.RequestExit();
                }

                return;
        }

        RefreshMenu();
    }

    private async Task ExecuteModuleCommandAsync(string commandId)
    {
        await _modules.ExecuteCommandAsync(MediaModuleId, commandId);
    }

    private void SetFullscreenStyle(FullscreenNotificationStyle style) =>
        _settings.Update(settings => settings with
        {
            Fullscreen = settings.Fullscreen with { Style = style }
        });

    private void SetMonitorMode(MonitorSelectionMode mode) =>
        _settings.Update(settings => settings with
        {
            Monitor = settings.Monitor with { Mode = mode }
        });

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        UpdateTrayVisibility();
        RefreshMenu();
    }

    private void OnModulePresentationChanged(object? sender, ModulePresentation? presentation) => RefreshMenu();

    private void OnMediaSnapshotChanged(object? sender, MediaSnapshot snapshot) => RefreshMenu();

    private void OnMediaSourcesChanged(object? sender, IReadOnlyList<MediaSourceInfo> sources) => RefreshMenu();

    private void OnDisplaysChanged(object? sender, IReadOnlyList<DisplayDescriptor> displays) => RefreshMenu();

    private void OnFocusChanged(object? sender, FocusChangedEventArgs args)
    {
        UpdateTrayVisibility();
        RefreshMenu();
    }

    private void OnLanguageChanged(object? sender, EventArgs args) => RefreshMenu();

    private void UpdateTrayVisibility()
    {
        var settings = _settings.Current;
        var visible = settings.Tray.ShowIcon ||
                      FocusAccessPolicy.RequiresTrayEscape(
                          _focus.Current,
                          settings.General.VisibilityMode);
        if (_tray.IsVisible != visible)
        {
            _tray.SetVisible(visible);
        }
    }

    private string FocusProfileName(FocusProfile profile)
    {
        if (profile.Kind == FocusProfileKind.Custom)
        {
            return string.IsNullOrWhiteSpace(profile.CustomName)
                ? Text("Focus.Title")
                : profile.CustomName;
        }

        return Text(FocusProfileDefaults.GetDisplayNameKey(profile));
    }

    private string Text(string key, params object?[] arguments) =>
        _localization.Get(key, arguments);
}
