using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.Modules.Time.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class TimerHoverView : UserControl
{
    private TimeToolsViewModel? _viewModel;

    public TimerHoverView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs args) => AttachViewModel(DataContext);

    private void OnUnloaded(object sender, RoutedEventArgs args) => DetachViewModel();

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) =>
        AttachViewModel(args.NewValue);

    private void AttachViewModel(object? dataContext)
    {
        if (ReferenceEquals(_viewModel, dataContext))
        {
            RefreshActions();
            return;
        }

        DetachViewModel();
        _viewModel = dataContext as TimeToolsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        RefreshActions();
    }

    private void DetachViewModel()
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(TimeToolsViewModel.Current) or
            nameof(TimeToolsViewModel.IsTimerCompleted))
        {
            RefreshActions();
        }
    }

    private void RefreshActions()
    {
        var alarmCanBeSilenced = _viewModel?.IsTimerCompleted == true;
        NormalActions.Visibility = alarmCanBeSilenced ? Visibility.Collapsed : Visibility.Visible;
        SilenceAlarmButton.Visibility = alarmCanBeSilenced ? Visibility.Visible : Visibility.Collapsed;
    }
}
