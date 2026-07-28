using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace MiaDock.App.Views.Settings;

public sealed partial class HomeSettingsPage : UserControl
{
    public HomeSettingsPage() => InitializeComponent();

    public event EventHandler<string>? NavigationRequested;

    private void OnNavigateClick(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { Tag: string tag })
        {
            NavigationRequested?.Invoke(this, tag);
        }
    }
}
