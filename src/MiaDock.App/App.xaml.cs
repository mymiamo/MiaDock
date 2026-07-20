using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using MiaDock.App.Bootstrapper;
using MiaDock.Core.Theming;
using MiaDock.UI.Services;

namespace MiaDock.App;

public partial class App : Application
{
    private readonly ServiceProvider _services;
    private Window? _window;

    public App()
    {
        try
        {
            InitializeComponent();
            _services = ServiceRegistration.BuildServiceProvider();
        }
        catch (Exception exception)
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "miadock-launch-error.log"), exception.ToString());
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _services.GetRequiredService<IThemeService>().Apply(ThemeStyle.AppleLike);
            _window = _services.GetRequiredService<MainWindow>();
            _window.Activate();
        }
        catch (Exception exception)
        {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "miadock-launch-error.log"), exception.ToString());
            throw;
        }
    }
}
