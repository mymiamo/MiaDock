using MiaDock.App.ViewModels;

namespace MiaDock.App.Services;

public sealed class SettingsWindowService(
    SettingsViewModel viewModel,
    ISettingsService settings,
    IApplicationLifetimeService lifetime,
    DiagnosticsViewModel diagnosticsViewModel,
    IDiagnosticsFileService diagnosticsFileService,
    IAppLocalizationService localization) : ISettingsWindowService
{
    private SettingsWindow? _window;

    public bool IsVisible => _window?.AppWindow.IsVisible == true;

    public void Show()
    {
        if (_window is null)
        {
            _window = new SettingsWindow(
                viewModel,
                settings,
                lifetime,
                diagnosticsViewModel,
                diagnosticsFileService,
                localization);
            _window.Closed += OnWindowClosed;
        }

        _window.AppWindow.Show();
        _window.Activate();
    }

    public void Hide() => _window?.AppWindow.Hide();

    public void CloseForShutdown()
    {
        if (_window is null)
        {
            return;
        }

        _window.Closed -= OnWindowClosed;
        _window.AllowCloseAndClose();
        _window = null;
    }

    private void OnWindowClosed(object sender, Microsoft.UI.Xaml.WindowEventArgs args)
    {
        if (_window is not null)
        {
            _window.Closed -= OnWindowClosed;
            _window = null;
        }
    }
}
