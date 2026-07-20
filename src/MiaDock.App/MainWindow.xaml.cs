using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using MiaDock.App.ViewModels;
using Windows.Graphics;

namespace MiaDock.App;

public sealed partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        Root.DataContext = viewModel;
        AppWindow.Resize(new SizeInt32(1080, 720));
    }
}
