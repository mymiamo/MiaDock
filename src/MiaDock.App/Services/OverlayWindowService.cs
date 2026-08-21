using Microsoft.Extensions.DependencyInjection;
namespace MiaDock.App.Services;

public sealed class OverlayWindowService : IOverlayWindowService, IDisposable
{
    private readonly IServiceProvider _services;
    private OverlayWindow? _window;
    private bool _disposed;

    public OverlayWindowService(IServiceProvider services)
    {
        _services = services;
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
        CloseForShutdown();
    }

    private OverlayWindow CreateWindow() => _services.GetRequiredService<OverlayWindow>();
}
