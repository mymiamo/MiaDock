using Windows.System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MiaDock.Modules.Media.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class MediaTimeline : UserControl
{
    private bool _isPointerSeeking;

    public MediaTimeline()
    {
        InitializeComponent();
        TimelineSlider.AddHandler(
            UIElement.PointerPressedEvent,
            new PointerEventHandler(OnPointerPressed),
            true);
        TimelineSlider.AddHandler(
            UIElement.PointerReleasedEvent,
            new PointerEventHandler(OnPointerReleased),
            true);
        TimelineSlider.PointerCaptureLost += OnPointerCaptureLost;
        TimelineSlider.KeyUp += OnKeyUp;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        if (TimelineSlider.IsEnabled)
        {
            _isPointerSeeking = true;
        }
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs args) => CommitPointerSeek();

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs args) => CommitPointerSeek();

    private void CommitPointerSeek()
    {
        if (!_isPointerSeeking)
        {
            return;
        }

        _isPointerSeeking = false;
        ExecuteSeek();
    }

    private void OnKeyUp(object sender, KeyRoutedEventArgs args)
    {
        if (args.Key is VirtualKey.Left or VirtualKey.Right or VirtualKey.Home or VirtualKey.End or
            VirtualKey.PageUp or VirtualKey.PageDown)
        {
            ExecuteSeek();
        }
    }

    private void ExecuteSeek()
    {
        if (DataContext is MusicModuleViewModel viewModel &&
            viewModel.SeekCommand.CanExecute(TimelineSlider.Value))
        {
            viewModel.SeekCommand.Execute(TimelineSlider.Value);
        }
    }
}
