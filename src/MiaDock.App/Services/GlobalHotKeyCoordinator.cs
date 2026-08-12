using MiaDock.Core.Modules;
using MiaDock.Core.Logging;
using MiaDock.Core.Settings;
using MiaDock.Core.Threading;
using MiaDock.Platform.Windows.HotKeys;

namespace MiaDock.App.Services;

public sealed class GlobalHotKeyCoordinator : IDisposable
{
    private readonly IGlobalHotKeyService _hotKeys;
    private readonly ISettingsService _settings;
    private readonly IIslandModuleRegistry _modules;
    private readonly OverlayWindow _overlay;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogService _log;
    private bool _started;

    public GlobalHotKeyCoordinator(
        IGlobalHotKeyService hotKeys,
        ISettingsService settings,
        IIslandModuleRegistry modules,
        OverlayWindow overlay,
        IUiDispatcher dispatcher,
        ILogService log)
    {
        _hotKeys = hotKeys;
        _settings = settings;
        _modules = modules;
        _overlay = overlay;
        _dispatcher = dispatcher;
        _log = log;
    }

    public void Start()
    {
        if (_started) return;
        _hotKeys.Invoked += OnInvoked;
        _settings.SettingsChanged += OnSettingsChanged;
        ApplySafely(_settings.Current.HotKeys);
        _started = true;
    }

    public void Dispose()
    {
        if (!_started) return;
        _hotKeys.Invoked -= OnInvoked;
        _settings.SettingsChanged -= OnSettingsChanged;
        _started = false;
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        if (args.Previous.HotKeys != args.Current.HotKeys)
        {
            ApplySafely(args.Current.HotKeys);
        }
    }

    private void OnInvoked(object? sender, HotKeyAction action)
    {
        if (!_dispatcher.TryEnqueue(() => _ = ExecuteSafelyAsync(action)))
        {
            _log.Write(
                TechnicalLogLevel.Warning,
                TechnicalEventIds.HotKeyDispatchFailed,
                "HotKeys",
                "A global hotkey command could not be dispatched to the UI thread.",
                properties: new Dictionary<string, object?> { ["action"] = action.ToString() });
        }
    }

    private async Task ExecuteSafelyAsync(HotKeyAction action)
    {
        try
        {
            switch (action)
            {
                case HotKeyAction.ToggleDock:
                    _overlay.ToggleDock();
                    break;
                case HotKeyAction.ToggleExpanded:
                    _overlay.ToggleExpandedFromShortcut();
                    break;
                case HotKeyAction.NextModule:
                    _overlay.SelectNextModuleFromShortcut();
                    break;
                case HotKeyAction.MediaPlayPause:
                    await _modules.ExecuteCommandAsync("media", "play-pause");
                    break;
                case HotKeyAction.TimerPauseResume:
                    await _modules.ExecuteCommandAsync("timer", "pause-resume");
                    break;
            }
        }
        catch (Exception exception)
        {
            _log.Write(
                TechnicalLogLevel.Error,
                TechnicalEventIds.HotKeyCommandFailed,
                "HotKeys",
                "A global hotkey command failed.",
                exception,
                new Dictionary<string, object?> { ["action"] = action.ToString() });
        }
    }

    private void ApplySafely(GlobalHotKeySettings settings)
    {
        try
        {
            _hotKeys.Apply(settings);
        }
        catch (Exception exception)
        {
            _log.Write(
                TechnicalLogLevel.Warning,
                TechnicalEventIds.HotKeyRegistrationFailed,
                "HotKeys",
                "Global hotkey settings could not be applied.",
                exception,
                new Dictionary<string, object?> { ["operation"] = "apply" });
        }
    }
}
