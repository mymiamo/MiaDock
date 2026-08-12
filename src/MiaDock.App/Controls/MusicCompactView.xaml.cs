using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Animations;
using MiaDock.App.Services;
using MiaDock.Modules.Media.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class MusicCompactView : UserControl, IModuleViewActivationAware
{
    private MusicModuleViewModel? _viewModel;
    private long _visibilityCallbackToken;
    private bool _isPresentationActive;
    private readonly ToolkitAnimationFactory _animations = new();

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
        if (_viewModel is not null)
        {
            _viewModel.TrackChanged -= OnTrackChanged;
        }
        _viewModel?.SetAudioMeterActive(this, false);

        _viewModel = viewModel;
        if (_viewModel is not null)
        {
            _viewModel.TrackChanged += OnTrackChanged;
        }
    }

    private void OnTrackChanged(object? sender, MiaDock.Modules.Media.Models.TrackIdentity args)
    {
        if (!IsLoaded || !_isPresentationActive || Visibility != Visibility.Visible)
        {
            return;
        }

        _ = AnimateTrackChangeAsync();
    }

    private async Task AnimateTrackChangeAsync()
    {
        try
        {
            await _animations.AnimateMicroFeedbackAsync(MusicIdentity);
        }
        catch
        {
            // Live media metadata must never interfere with presentation.
        }
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
