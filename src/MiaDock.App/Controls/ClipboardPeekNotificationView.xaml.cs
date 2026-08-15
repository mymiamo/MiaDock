using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.ViewModels;
using MiaDock.Core.Modules;

namespace MiaDock.App.Controls;

public sealed partial class ClipboardPeekNotificationView : UserControl
{
    private readonly IIslandModuleRegistry _modules;

    public ClipboardPeekNotificationView(IIslandModuleRegistry modules, ClipboardPeekViewModel peek)
    {
        _modules = modules;
        Peek = peek;
        InitializeComponent();
    }

    public ClipboardPeekViewModel Peek { get; }

    private async void OnCommandClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: string commandId, IsEnabled: true })
            await _modules.ExecuteCommandAsync("clipboard-peek", commandId);
    }
}
