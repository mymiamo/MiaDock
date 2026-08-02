using System.ComponentModel;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using MiaDock.App.ViewModels;
using MiaDock.Core.Presentation;
namespace MiaDock.App.Views.Settings;

public sealed partial class AppearanceSettingsPage : UserControl
{
    private PreviewState _previewState = PreviewState.Compact;
    private INotifyPropertyChanged? _observedViewModel;
    private CancellationTokenSource? _testCancellation;

    public AppearanceSettingsPage() => InitializeComponent();

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        ObserveDataContext();
        UpdatePreview(animate: false);
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        StopObserving();
        _testCancellation?.Cancel();
        _testCancellation?.Dispose();
        _testCancellation = null;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (IsLoaded)
        {
            ObserveDataContext();
            UpdatePreview(animate: false);
        }
    }

    private void ObserveDataContext()
    {
        StopObserving();
        _observedViewModel = DataContext as INotifyPropertyChanged;
        if (_observedViewModel is not null)
        {
            _observedViewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void StopObserving()
    {
        if (_observedViewModel is not null)
        {
            _observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _observedViewModel = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(SettingsViewModel.CollapsedWidth) or
            nameof(SettingsViewModel.CollapsedHeight) or
            nameof(SettingsViewModel.HoverWidth) or
            nameof(SettingsViewModel.HoverHeight) or
            nameof(SettingsViewModel.ExpandedWidth) or
            nameof(SettingsViewModel.ExpandedHeight) or
            nameof(SettingsViewModel.CornerRadius) or
            nameof(SettingsViewModel.Theme) or
            nameof(SettingsViewModel.MotionPreset) or
            nameof(SettingsViewModel.MotionIntensity) or
            nameof(SettingsViewModel.AnimationSpeed))
        {
            UpdatePreview(animate: false);
        }
    }

    private void OnPreviewModeClick(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: string value } &&
            Enum.TryParse<PreviewState>(value, out var state))
        {
            _previewState = state;
            UpdatePreview(animate: true);
        }
    }

    private async void OnTestAnimationClick(object sender, RoutedEventArgs args)
    {
        _testCancellation?.Cancel();
        _testCancellation?.Dispose();
        _testCancellation = new CancellationTokenSource();
        var cancellationToken = _testCancellation.Token;
        try
        {
            foreach (var state in new[] { PreviewState.Compact, PreviewState.Hover, PreviewState.Expanded })
            {
                _previewState = state;
                UpdatePreview(animate: true);
                await Task.Delay(TimeSpan.FromMilliseconds(650), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void UpdatePreview(bool animate)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var (width, height) = _previewState switch
        {
            PreviewState.Hover => (viewModel.HoverWidth * 0.72, viewModel.HoverHeight * 0.72),
            PreviewState.Expanded => (viewModel.ExpandedWidth * 0.72, viewModel.ExpandedHeight * 0.62),
            _ => (viewModel.CollapsedWidth * 0.72, viewModel.CollapsedHeight * 0.72)
        };
        PreviewDock.Width = Math.Clamp(width, 120, 470);
        PreviewDock.Height = Math.Clamp(height, 32, 195);
        PreviewDock.CornerRadius = new CornerRadius(Math.Min(
            viewModel.CornerRadius * 0.72,
            PreviewDock.Height / 2));

        CompactPreviewContent.Visibility = _previewState == PreviewState.Compact
            ? Visibility.Visible
            : Visibility.Collapsed;
        HoverPreviewContent.Visibility = _previewState == PreviewState.Hover
            ? Visibility.Visible
            : Visibility.Collapsed;
        ExpandedPreviewContent.Visibility = _previewState == PreviewState.Expanded
            ? Visibility.Visible
            : Visibility.Collapsed;
        CompactPreviewButton.IsEnabled = _previewState != PreviewState.Compact;
        HoverPreviewButton.IsEnabled = _previewState != PreviewState.Hover;
        ExpandedPreviewButton.IsEnabled = _previewState != PreviewState.Expanded;

        if (animate && viewModel.MotionPreset != MotionPreset.Off)
        {
            AnimatePreview(viewModel);
        }
    }

    private void AnimatePreview(SettingsViewModel viewModel)
    {
        var visual = ElementCompositionPreview.GetElementVisual(PreviewDock);
        visual.StopAnimation(nameof(visual.Opacity));
        visual.StopAnimation(nameof(visual.Scale));
        visual.CenterPoint = new Vector3(
            (float)(PreviewDock.ActualWidth / 2),
            (float)(PreviewDock.ActualHeight / 2),
            0);
        var duration = TimeSpan.FromMilliseconds(220 / Math.Clamp(viewModel.AnimationSpeed, 0.5, 2));
        var intensity = (float)Math.Clamp(viewModel.MotionIntensity, 0, 1);
        var scale = 1 - 0.035f * intensity;
        var easing = visual.Compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.16f, 1),
            new Vector2(0.3f, 1));
        StartScalar(visual, nameof(visual.Opacity), 0.35f, 1, duration, easing);
        StartVector(visual, nameof(visual.Scale), new Vector3(scale, scale, 1), Vector3.One, duration, easing);
    }

    private static void StartScalar(
        Visual visual,
        string property,
        float from,
        float to,
        TimeSpan duration,
        CompositionEasingFunction easing)
    {
        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.Duration = duration;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, easing);
        visual.StartAnimation(property, animation);
    }

    private static void StartVector(
        Visual visual,
        string property,
        Vector3 from,
        Vector3 to,
        TimeSpan duration,
        CompositionEasingFunction easing)
    {
        var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
        animation.Duration = duration;
        animation.InsertKeyFrame(0, from);
        animation.InsertKeyFrame(1, to, easing);
        visual.StartAnimation(property, animation);
    }

    private enum PreviewState
    {
        Compact,
        Hover,
        Expanded
    }
}
