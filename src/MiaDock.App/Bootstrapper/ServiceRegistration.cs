using Microsoft.Extensions.DependencyInjection;
using MiaDock.App.ViewModels;
using MiaDock.Core.Modules;
using MiaDock.Core.Presentation;
using MiaDock.Modules.Media;
using MiaDock.Modules.Media.Services;
using MiaDock.Modules.Media.ViewModels;
using MiaDock.UI.Presentation;
using MiaDock.UI.Services;

namespace MiaDock.App.Bootstrapper;

public static class ServiceRegistration
{
    public static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IIslandStateMachine, IslandStateMachine>();
        services.AddSingleton<IFakeMediaService, FakeMediaService>();
        services.AddSingleton<IIslandModule, MusicModule>();
        services.AddSingleton<IThemeService, ThemeService>();

        services.AddSingleton<MusicModuleViewModel>();
        services.AddSingleton<IslandViewModel>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider(validateScopes: true);
    }
}
