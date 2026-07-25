using MiaDock.Core.Modules;
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
    private bool _started;

    public GlobalHotKeyCoordinator(
        IGlobalHotKeyService hotKeys,
        ISettingsService settings,
        IIslandModuleRegistry modules,
        OverlayWindow overlay,
        IUiDispatcher dispatcher)
    {
        _hotKeys = hotKeys;
        _settings = settings;
        _modules = modules;
        _overlay = overlay;
        _dispatcher = dispatcher;
    }

    public void Start()
    {
        if (_started) return;
        _hotKeys.Invoked += OnInvoked;
        _settings.SettingsChanged += OnSettingsChanged;
        _hotKeys.Apply(_settings.Current.HotKeys);
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
            _hotKeys.Apply(args.Current.HotKeys);
        }
    }

    private void OnInvoked(object? sender, HotKeyAction action) =>
        _dispatcher.TryEnqueue(() => Execute(action));

    private void Execute(HotKeyAction action)
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
                _ = _modules.ExecuteCommandAsync("media", "play-pause");
                break;
            case HotKeyAction.TimerPauseResume:
                _ = _modules.ExecuteCommandAsync("timer", "pause-resume");
                break;
        }
    }
}
