using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.Core.Modules;

namespace MiaDock.App.Controls;

public sealed partial class DeviceHubNotificationView : UserControl
{
    private readonly IIslandModuleRegistry _modules;

    public DeviceHubNotificationView(IIslandModuleRegistry modules)
    {
        _modules = modules;
        InitializeComponent();
    }

    private async void OnCommandClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: string commandId, IsEnabled: true })
        {
            await _modules.ExecuteCommandAsync("device-hub", commandId);
        }
    }
}
