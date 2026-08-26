using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
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

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        AttachViewModel();
        UpdateTimerRing();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) => DetachViewModel();

    private void OnDurationEditorGotFocus(object sender, RoutedEventArgs args) =>
        DockInteractionSession.Begin(sender);

    private void OnDurationEditorLostFocus(object sender, RoutedEventArgs args) =>
        DockInteractionSession.End(sender);

    private void OnPresetClick(object sender, RoutedEventArgs args)
    {
        if (sender is ToggleButton { DataContext: TimerPresetOption preset } &&
            _viewModel?.SelectPresetCommand.CanExecute(preset) == true)
        {
            _viewModel.SelectPresetCommand.Execute(preset);
        }
    }

    private void OnTimerRingSizeChanged(object sender, SizeChangedEventArgs args) =>
        UpdateTimerRing();

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        DetachViewModel();
        AttachViewModel();
    }

    private void AttachViewModel()
    {
        if (_viewModel == DataContext as TimeToolsViewModel)
        {
            ApplyLocalization();
            return;
        }

        _viewModel = DataContext as TimeToolsViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateTimerRing();
        ApplyLocalization();
    }

    private void DetachViewModel()
    {
        DockInteractionSession.End(HoursBox);
        DockInteractionSession.End(MinutesBox);
        DockInteractionSession.End(SecondsBox);
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(TimeToolsViewModel.TimerProgressPercent))
        {
            UpdateTimerRing();
        }
    }

    private void UpdateTimerRing()
    {
        if (TimerProgressArc is null || TimerRing is null)
        {
            return;
        }

        var progress = Math.Clamp((_viewModel?.TimerProgressPercent ?? 0) / 100, 0, 1);
        TimerProgressArc.Visibility = progress > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (progress <= 0)
        {
            return;
        }

        var diameter = Math.Min(TimerRing.ActualWidth, TimerRing.ActualHeight);
        if (diameter <= TimerProgressArc.StrokeThickness)
        {
            return;
        }

        // Shape dash lengths are expressed in stroke-width units. One dash and
        // one gap therefore describe exactly one circle at every DPI and size.
        var circumference = Math.PI * (diameter - TimerProgressArc.StrokeThickness);
        var normalizedCircumference = circumference / TimerProgressArc.StrokeThickness;
        var dash = normalizedCircumference * progress;
        TimerProgressArc.StrokeDashArray = new DoubleCollection
        {
            dash,
            Math.Max(normalizedCircumference - dash, 0.001)
        };
    }

    private void ApplyLocalization()
    {
        if (_localization is not null)
        {
            DispatcherQueue.TryEnqueue(() => _localization.Apply(this));
        }
    }
}
