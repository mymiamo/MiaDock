using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.Modules.Media.Models;
using MiaDock.Modules.Media.ViewModels;
using Windows.UI.ViewManagement;

namespace MiaDock.App.Controls;

public sealed partial class CollapsedIslandView : UserControl
{
    private readonly UISettings _uiSettings = new();
    private MusicModuleViewModel? _viewModel;
    private long _visibilityCallbackToken;

    public CollapsedIslandView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _visibilityCallbackToken = RegisterPropertyChangedCallback(VisibilityProperty, OnVisibilityChanged);
        AttachViewModel(DataContext as MusicModuleViewModel);
        UpdateVisualizer();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        VisualizerStoryboard.Stop();
        AttachViewModel(null);

        if (_visibilityCallbackToken != 0)
        {
            UnregisterPropertyChangedCallback(VisibilityProperty, _visibilityCallbackToken);
            _visibilityCallbackToken = 0;
        }
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (IsLoaded)
        {
            AttachViewModel(args.NewValue as MusicModuleViewModel);
            UpdateVisualizer();
        }
    }

    private void AttachViewModel(MusicModuleViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MusicModuleViewModel.Current))
        {
            UpdateVisualizer();
        }
    }

    private void OnVisibilityChanged(DependencyObject sender, DependencyProperty property) =>
        UpdateVisualizer();

    private void UpdateVisualizer()
    {
        var shouldAnimate = IsLoaded
            && Visibility == Visibility.Visible
            && _uiSettings.AnimationsEnabled
            && _viewModel?.Current.PlaybackStatus == PlaybackStatus.Playing;

        if (shouldAnimate)
        {
            VisualizerStoryboard.Begin();
            return;
        }

        VisualizerStoryboard.Stop();
    }
}
