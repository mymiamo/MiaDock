using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.Modules.DeviceStatus.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class DeviceHubExpandedView : UserControl
{
    private Expander? _expandedDevice;
    private DeviceHubViewModel? _viewModel;

    public DeviceHubExpandedView() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (DataContext is DeviceHubViewModel viewModel && !ReferenceEquals(_viewModel, viewModel))
        {
            if (_viewModel is not null)
            {
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _viewModel = viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateDynamicState();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
        _expandedDevice = null;
    }

    private void OnDeviceExpanding(Expander sender, ExpanderExpandingEventArgs args)
    {
        if (_expandedDevice is not null && !ReferenceEquals(_expandedDevice, sender))
        {
            _expandedDevice.IsExpanded = false;
        }
        _expandedDevice = sender;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(DeviceHubViewModel.BatteryDevices) or
            nameof(DeviceHubViewModel.StorageOperationError) or
            nameof(DeviceHubViewModel.StorageOperationOpen) or
            nameof(DeviceHubViewModel.BluetoothOperationError) or
            nameof(DeviceHubViewModel.BluetoothOperationOpen))
        {
            UpdateDynamicState();
        }
    }

    private void UpdateDynamicState()
    {
        if (_viewModel is null)
        {
            return;
        }

        BatterySection.Visibility = _viewModel.BatteryDevices.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        StorageInfoBar.Severity = _viewModel.StorageOperationError
            ? InfoBarSeverity.Error
            : InfoBarSeverity.Success;
        BluetoothInfoBar.Severity = _viewModel.BluetoothOperationError
            ? InfoBarSeverity.Error
            : InfoBarSeverity.Success;
    }
}
