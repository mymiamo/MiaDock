using Microsoft.Extensions.DependencyInjection;
using MiaDock.Core.Settings;
using MiaDock.Core.Threading;

namespace MiaDock.App.Services;

public sealed class OverlayWindowService : IOverlayWindowService, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly ISettingsService _settings;
    private readonly IUiDispatcher _dispatcher;
    private OverlayWindow? _window;
    private int _themeRestartPending;
    private bool _disposed;

    public OverlayWindowService(
        IServiceProvider services,
        ISettingsService settings,
        IUiDispatcher dispatcher)
    {
        _services = services;
        _settings = settings;
        _dispatcher = dispatcher;
        _settings.SettingsChanged += OnSettingsChanged;
    }

    public OverlayWindow Current => _window ??= CreateWindow();

    public bool IsDockVisible => Current.IsDockVisible;

    public void ShowNoActivate() => Current.ShowNoActivate();

    public void ShowDock() => Current.ShowDock();

    public void HideDock() => Current.HideDock();

    public void ToggleDock() => Current.ToggleDock();

    public void ToggleExpandedFromShortcut() => Current.ToggleExpandedFromShortcut();

    public void SelectNextModuleFromShortcut() => Current.SelectNextModuleFromShortcut();

    public void CloseForShutdown()
    {
        var window = _window;
        _window = null;
        window?.Close();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settings.SettingsChanged -= OnSettingsChanged;
        CloseForShutdown();
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        if (_disposed || args.Previous.Appearance.Theme == args.Current.Appearance.Theme ||
            Interlocked.Exchange(ref _themeRestartPending, 1) != 0)
        {
            return;
        }

        // SettingsChanged is raised while existing listeners are still updating
        // their state. Queue the restart onto the next UI turn so the old dock
        // can finish that callback before its HWND and visual tree are released.
        if (!_dispatcher.TryEnqueue(RestartAfterThemeChange))
        {
            Interlocked.Exchange(ref _themeRestartPending, 0);
        }
    }

    private void RestartAfterThemeChange()
    {
        try
        {
            if (_disposed || _window is null)
            {
                return;
            }

            var wasVisible = _window.IsDockVisible;
            var previous = _window;
            _window = null;
            previous.Close();

            var replacement = Current;
            if (wasVisible)
            {
                replacement.ShowNoActivate();
            }
        }
        finally
        {
            Interlocked.Exchange(ref _themeRestartPending, 0);
        }
    }

    private OverlayWindow CreateWindow() => _services.GetRequiredService<OverlayWindow>();
}
