using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.Core.Updates;

namespace MiaDock.App.Controls;

public sealed partial class StoreUpdateNotificationView : UserControl
{
    private readonly IStoreUpdateService? _storeUpdates;

    public StoreUpdateNotificationView(IStoreUpdateService? storeUpdates = null)
    {
        _storeUpdates = storeUpdates;
        InitializeComponent();
    }

    private async void OnOpenStoreClick(object sender, RoutedEventArgs args)
    {
        if (_storeUpdates is not null)
        {
            await _storeUpdates.OpenStorePageAsync();
        }
    }
}
