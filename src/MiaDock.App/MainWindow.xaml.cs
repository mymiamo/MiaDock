using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MiaDock.App.ViewModels;
using MiaDock.App.Services;
using Windows.Graphics;
using MiaDock.App.Infrastructure;

namespace MiaDock.App;

public sealed partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel, IModuleViewRegistry moduleViews)
    {
        InitializeComponent();
        WindowBranding.ApplyIcon(this);
        Root.DataContext = viewModel;
        PreviewIsland.ConfigureModuleViews(moduleViews);
        AppWindow.Resize(new SizeInt32(1080, 720));
    }
}
