using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Services;
using MiaDock.Modules.Media.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class MusicCompactView : UserControl, IModuleViewActivationAware
{
    private MusicModuleViewModel? _viewModel;
    private long _visibilityCallbackToken;
    private bool _isPresentationActive;

    public MusicCompactView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _visibilityCallbackToken = RegisterPropertyChangedCallback(VisibilityProperty, OnVisibilityChanged);
        AttachViewModel(DataContext as MusicModuleViewModel);
        UpdateMeterActivity();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _viewModel?.SetAudioMeterActive(this, false);
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
            UpdateMeterActivity();
        }
    }

    private void AttachViewModel(MusicModuleViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel)) return;
        _viewModel?.SetAudioMeterActive(this, false);

        _viewModel = viewModel;
    }

    private void OnVisibilityChanged(DependencyObject sender, DependencyProperty property) => UpdateMeterActivity();

    public void SetPresentationActive(bool isActive)
    {
        _isPresentationActive = isActive;
        UpdateMeterActivity();
    }

    private void UpdateMeterActivity() =>
        _viewModel?.SetAudioMeterActive(
            this,
            IsLoaded && _isPresentationActive && Visibility == Visibility.Visible);

}
