using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Services;
using MiaDock.Modules.Time.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class TimerExpandedView : UserControl
{
    private readonly IAppLocalizationService? _localization;
    private TimeToolsViewModel? _viewModel;

    public TimerExpandedView(IAppLocalizationService? localization = null)
    {
        _localization = localization;
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs args) => AttachViewModel();

    private void OnUnloaded(object sender, RoutedEventArgs args) => DetachViewModel();

    private void OnDurationEditorGotFocus(object sender, RoutedEventArgs args) =>
        DockInteractionSession.Begin(sender);

    private void OnDurationEditorLostFocus(object sender, RoutedEventArgs args) =>
        DockInteractionSession.End(sender);

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        DetachViewModel();
        AttachViewModel();
    }

    private void AttachViewModel()
    {
        if (_viewModel == DataContext as TimeToolsViewModel)
        {
            RefreshSelection();
            return;
        }

        _viewModel = DataContext as TimeToolsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
        RefreshSelection();
    }

    private void DetachViewModel()
    {
        DockInteractionSession.End(HoursBox);
        DockInteractionSession.End(MinutesBox);
        DockInteractionSession.End(SecondsBox);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
    }

    private void OnTimerSegmentClick(object sender, RoutedEventArgs args)
    {
        if (_viewModel is not null) _viewModel.SelectedToolIndex = 0;
        RefreshSelection();
    }

    private void OnStopwatchSegmentClick(object sender, RoutedEventArgs args)
    {
        if (_viewModel is not null) _viewModel.SelectedToolIndex = 1;
        RefreshSelection();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(TimeToolsViewModel.SelectedToolIndex))
        {
            RefreshSelection();
        }
    }

    private void RefreshSelection()
    {
        var stopwatch = _viewModel?.SelectedToolIndex == 1;
        TimerSegment.IsChecked = !stopwatch;
        StopwatchSegment.IsChecked = stopwatch;
        TimerPanel.Visibility = stopwatch ? Visibility.Collapsed : Visibility.Visible;
        StopwatchPanel.Visibility = stopwatch ? Visibility.Visible : Visibility.Collapsed;
        if (_localization is not null)
        {
            DispatcherQueue.TryEnqueue(() => _localization.Apply(this));
        }
    }
}
