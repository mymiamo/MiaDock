using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Animations;
using MiaDock.App.Services;
using MiaDock.Modules.Media.Models;
using MiaDock.Modules.Media.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class MusicCompactView : UserControl, IModuleViewActivationAware
{
    private MusicModuleViewModel? _viewModel;
    private readonly DispatcherQueueTimer _trackTransitionTimer;
    private long _visibilityCallbackToken;
    private bool _isPresentationActive;
    private bool _isTrackTransition;
    private TrackIdentity? _displayedTrack;

    public MusicCompactView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        _trackTransitionTimer = DispatcherQueue.CreateTimer();
        _trackTransitionTimer.IsRepeating = false;
        _trackTransitionTimer.Interval = TimeSpan.FromMilliseconds(650);
        _trackTransitionTimer.Tick += OnTrackTransitionElapsed;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_visibilityCallbackToken != 0) return;
        _visibilityCallbackToken = RegisterPropertyChangedCallback(VisibilityProperty, OnVisibilityChanged);
        AttachViewModel(DataContext as MusicModuleViewModel);
        UpdateMeterActivity();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _trackTransitionTimer.Stop();
        _isTrackTransition = false;
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
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;
        _displayedTrack = viewModel is null ? null : TrackIdentity.From(viewModel.Current);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdatePlaybackVisual();
    }

    private void OnVisibilityChanged(DependencyObject sender, DependencyProperty property) => UpdateMeterActivity();

    public void SetPresentationActive(bool isActive)
    {
        _isPresentationActive = isActive;
        UpdateMeterActivity();
    }

    private void UpdateMeterActivity()
    {
        var active = IsLoaded && _isPresentationActive && Visibility == Visibility.Visible;
        MusicActivity.IsSuppressed = !active || _isTrackTransition;
        if (!active) { _trackTransitionTimer.Stop(); _isTrackTransition = false; }
        _viewModel?.SetAudioMeterActive(
            this,
            active);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(MusicModuleViewModel.Current) || _viewModel is null)
        {
            return;
        }

        var nextTrack = TrackIdentity.From(_viewModel.Current);
        if (_displayedTrack is not null && nextTrack is not null && nextTrack != _displayedTrack)
        {
            StartTrackTransition();
        }

        _displayedTrack = nextTrack;
        UpdatePlaybackVisual();
    }

    private void StartTrackTransition()
    {
        if (!_isPresentationActive || !IsLoaded) return;
        _isTrackTransition = true;
        _trackTransitionTimer.Stop();
        _trackTransitionTimer.Start();
        UpdatePlaybackVisual();
        MicroMotion.PlayAttention(ArtworkStateOverlay);
    }

    private void OnTrackTransitionElapsed(DispatcherQueueTimer sender, object args)
    {
        _isTrackTransition = false;
        UpdatePlaybackVisual();
    }

    private void UpdatePlaybackVisual()
    {
        var hasMedia = _viewModel?.Current.HasMedia == true;
        var isPaused = hasMedia && _viewModel?.IsPlaying != true;
        ArtworkStateOverlay.Visibility = _isTrackTransition || isPaused
            ? Visibility.Visible
            : Visibility.Collapsed;
        PausedGlyph.Visibility = isPaused && !_isTrackTransition
            ? Visibility.Visible
            : Visibility.Collapsed;
        TrackChangeGlyph.Visibility = _isTrackTransition
            ? Visibility.Visible
            : Visibility.Collapsed;
        MusicActivity.Visibility = Visibility.Visible;
        MusicActivity.IsSuppressed = _isTrackTransition || !_isPresentationActive || !IsLoaded || Visibility != Visibility.Visible;
    }

}
