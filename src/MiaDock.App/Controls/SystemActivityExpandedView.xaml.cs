using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using MiaDock.Modules.SystemStatus.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class SystemActivityExpandedView : UserControl
{
    public SystemActivityExpandedView() => InitializeComponent();

    private void OnMasterVolumeChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        if (DataContext is SystemActivityViewModel viewModel &&
            Math.Abs(args.NewValue - viewModel.Snapshot.MasterVolumePercent) >= 1)
        {
            _ = viewModel.SetMasterVolumeAsync(args.NewValue);
        }
    }

    private void OnApplicationVolumeChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        if (DataContext is SystemActivityViewModel viewModel &&
            viewModel.IsApplicationVolumeAvailable &&
            Math.Abs(args.NewValue - viewModel.Snapshot.ApplicationVolumePercent) >= 1)
        {
            _ = viewModel.SetApplicationVolumeAsync(args.NewValue);
        }
    }
}
