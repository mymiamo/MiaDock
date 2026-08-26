using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Animations;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class SystemActivityExpandedView : UserControl
{
    private readonly DispatcherTimer _durationTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private SystemActivityViewModel? _viewModel;
    private CallActivityState _lastState = CallActivityState.None;

    public SystemActivityExpandedView()
    {
        InitializeComponent();
        _durationTimer.Tick += OnDurationTick;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (DataContext is SystemActivityViewModel viewModel && !ReferenceEquals(_viewModel, viewModel))
        {
            if (_viewModel is not null) _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
        _lastState = _viewModel?.Snapshot.CallActivity.State ?? CallActivityState.None;
        UpdatePresentation(animate: _lastState == CallActivityState.Detected);
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        _durationTimer.Stop();
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
        _lastState = CallActivityState.None;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(SystemActivityViewModel.Snapshot)) return;
        var nextState = _viewModel?.Snapshot.CallActivity.State ?? CallActivityState.None;
        var animate = _lastState != CallActivityState.Detected && nextState == CallActivityState.Detected;
        _lastState = nextState;
        UpdatePresentation(animate);
    }

    private void UpdatePresentation(bool animate)
    {
        if (_viewModel?.IsCallDetected == true && IsLoaded)
        {
            UpdateDuration();
            _durationTimer.Start();
            if (animate)
            {
                MicroMotion.PlayNotification(DetectedIconContainer);
                MicroMotion.PlayAttention(DetectedHero);
            }
        }
        else
        {
            _durationTimer.Stop();
            DurationText.Text = "00:00";
        }
    }

    private void OnDurationTick(object? sender, object args) => UpdateDuration();

    private void UpdateDuration()
    {
        var startedAt = _viewModel?.Snapshot.CallActivity.StartedAt;
        if (startedAt is null)
        {
            DurationText.Text = "00:00";
            return;
        }

        var elapsed = DateTimeOffset.UtcNow - startedAt.Value;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;
        DurationText.Text = elapsed.TotalHours >= 1
            ? $"{(int)elapsed.TotalHours:00}:{elapsed.Minutes:00}:{elapsed.Seconds:00}"
            : $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";
    }
}
