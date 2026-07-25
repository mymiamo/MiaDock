using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.ViewModels;
using MiaDock.Modules.Media.Models;
using MiaDock.Modules.Media.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class IdleHoverView : UserControl
{
    private readonly MusicModuleViewModel? _music;
    private readonly IdleDashboardViewModel? _dashboard;
    private DispatcherQueueTimer? _minuteTimer;

    public IdleHoverView() : this(null, null)
    {
    }

    public IdleHoverView(MusicModuleViewModel? music, IdleDashboardViewModel? idleDashboard)
    {
        _music = music;
        _dashboard = idleDashboard;
        InitializeComponent();
        LayoutRoot.DataContext = idleDashboard;
        MusicRow.DataContext = music;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_music is not null)
        {
            _music.PropertyChanged += OnMusicPropertyChanged;
        }

        UpdateClock();
        UpdateContent();
        ScheduleNextMinute();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (_music is not null)
        {
            _music.PropertyChanged -= OnMusicPropertyChanged;
        }

        StopTimer();
    }

    private void OnMusicPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MusicModuleViewModel.Current))
        {
            UpdateContent();
        }
    }

    private void UpdateContent()
    {
        var showTrack = _music?.Current.HasMedia == true &&
                        _music.Current.PlaybackStatus == PlaybackStatus.Playing;
        MusicRow.Visibility = showTrack ? Visibility.Visible : Visibility.Collapsed;
        IdleRow.Visibility = showTrack ? Visibility.Collapsed : Visibility.Visible;
        UpdateAutomationName(showTrack);
    }

    private void UpdateClock()
    {
        var now = DateTimeOffset.Now;
        var culture = CultureInfo.CurrentCulture;
        TimeText.Text = now.ToString("HH:mm", culture);
        DateText.Text = now.ToString("ddd, d MMM", culture);
    }

    private void UpdateAutomationName(bool showTrack)
    {
        var culture = CultureInfo.CurrentCulture;
        var clock = DateTimeOffset.Now.ToString("HH:mm", culture);
        var detail = showTrack
            ? $"{_music!.Current.Track.Title}, {_music.Current.Track.Artist}"
            : _dashboard?.StatusSummary;
        AutomationProperties.SetName(LayoutRoot,
            string.IsNullOrWhiteSpace(detail) ? $"Saat {clock}" : $"Saat {clock}, {detail}");
    }

    private void OnMinuteElapsed(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        UpdateClock();
        UpdateContent();
        ScheduleNextMinute();
    }

    private void ScheduleNextMinute()
    {
        StopTimer();
        var now = DateTimeOffset.Now;
        var nextMinute = new DateTimeOffset(
            now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Offset).AddMinutes(1);

        _minuteTimer = DispatcherQueue.CreateTimer();
        _minuteTimer.IsRepeating = false;
        _minuteTimer.Interval = nextMinute - now;
        _minuteTimer.Tick += OnMinuteElapsed;
        _minuteTimer.Start();
    }

    private void StopTimer()
    {
        if (_minuteTimer is null)
        {
            return;
        }

        _minuteTimer.Stop();
        _minuteTimer.Tick -= OnMinuteElapsed;
        _minuteTimer = null;
    }
}
