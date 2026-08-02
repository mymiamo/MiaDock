using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using MiaDock.Modules.SystemStatus.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class VolumeNotificationView : UserControl
{
    public static readonly DependencyProperty ShowControlsProperty =
        DependencyProperty.Register(
            nameof(ShowControls),
            typeof(bool),
            typeof(VolumeNotificationView),
            new PropertyMetadata(false, OnShowControlsChanged));

    public VolumeNotificationView() => InitializeComponent();

    public bool ShowControls
    {
        get => (bool)GetValue(ShowControlsProperty);
        set => SetValue(ShowControlsProperty, value);
    }

    private static void OnShowControlsChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs args)
    {
        var view = (VolumeNotificationView)sender;
        var show = (bool)args.NewValue;
        view.VolumeProgress.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        view.VolumeSlider.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        view.SettingsButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnMasterVolumeChanged(
        object sender,
        RangeBaseValueChangedEventArgs args)
    {
        if (ShowControls &&
            DataContext is VolumeModuleViewModel viewModel &&
            Math.Abs(args.NewValue - viewModel.Snapshot.MasterVolumePercent) >= 1)
        {
            _ = viewModel.SetMasterVolumeAsync(args.NewValue);
        }
    }
}
