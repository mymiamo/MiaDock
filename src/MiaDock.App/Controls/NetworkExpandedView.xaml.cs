using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MiaDock.App.Services;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.ViewModels;
using Windows.UI;

namespace MiaDock.App.Controls;

public sealed partial class NetworkExpandedView : UserControl, IModuleViewActivationAware
{
    public NetworkExpandedView() => InitializeComponent();

    public void SetPresentationActive(bool isActive)
    {
        if (DataContext is NetworkModuleViewModel viewModel) viewModel.SetExpandedActive(isActive);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is NetworkModuleViewModel viewModel)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateNetworkColor();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is NetworkModuleViewModel viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(NetworkModuleViewModel.Snapshot))
        {
            UpdateNetworkColor();
        }
    }

    private void UpdateNetworkColor()
    {
        NetworkIcon.Foreground = (DataContext as NetworkModuleViewModel)?.Snapshot.Connectivity switch
        {
            NetworkConnectivityKind.Internet =>
                new SolidColorBrush(Color.FromArgb(255, 74, 222, 128)),
            NetworkConnectivityKind.ConstrainedInternet or NetworkConnectivityKind.LocalAccess =>
                new SolidColorBrush(Color.FromArgb(255, 245, 158, 11)),
            _ => Application.Current.Resources["IslandTextSecondaryBrush"] as Brush
                 ?? new SolidColorBrush(Color.FromArgb(180, 255, 255, 255))
        };
    }
}
