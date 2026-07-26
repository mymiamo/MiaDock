using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.ViewModels;
using MiaDock.Modules.Media.ViewModels;
using MiaDock.Modules.SystemStatus.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class IdleExpandedView : UserControl
{
    private readonly MusicModuleViewModel? _music;
    private readonly IdleDashboardViewModel? _dashboard;
    private DispatcherQueueTimer? _minuteTimer;
    private bool _isLoaded;

    public IdleExpandedView() : this(null, null, null)
    {
    }

    public IdleExpandedView(
        MusicModuleViewModel? music,
        SystemActivityViewModel? system,
        IdleDashboardViewModel? dashboard)
    {
        _music = music;
        _dashboard = dashboard;
        InitializeComponent();
        LayoutRoot.DataContext = dashboard;
        SystemStatusPanel.DataContext = system;
        MusicPanel.DataContext = music;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        if (_music is not null)
        {
            _music.PropertyChanged += OnMusicPropertyChanged;
        }

        UpdateClock();
        UpdateMediaVisibility();
        ScheduleNextMinute();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (!_isLoaded)
        {
            return;
        }

        _isLoaded = false;
        if (_music is not null)
        {
            _music.PropertyChanged -= OnMusicPropertyChanged;
        }

        StopTimer();
    }

    private void OnMusicPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(MusicModuleViewModel.Current) or
            nameof(MusicModuleViewModel.IsMediaAvailable))
        {
            UpdateMediaVisibility();
        }
    }

    private void UpdateMediaVisibility()
    {
        var hasMedia = _music?.Current.HasMedia == true;
        MusicPanel.Visibility = hasMedia ? Visibility.Visible : Visibility.Collapsed;
        IdlePanel.Visibility = hasMedia ? Visibility.Collapsed : Visibility.Visible;
        UpdateAutomationName(hasMedia);
    }

    private void UpdateClock()
    {
        var now = DateTimeOffset.Now;
        var culture = CultureInfo.CurrentCulture;
        TimeText.Text = now.ToString("HH:mm", culture);
        DateText.Text = now.ToString("dddd, d MMMM", culture);
    }

    private void UpdateAutomationName(bool hasMedia)
    {
        var time = DateTimeOffset.Now.ToString("HH:mm", CultureInfo.CurrentCulture);
        var detail = hasMedia
            ? $"{_music!.Current.Track.Title}, {_music.Current.Track.Artist}"
            : _dashboard?.StatusSummary;
        AutomationProperties.SetName(
            LayoutRoot,
            string.IsNullOrWhiteSpace(detail) ? $"Ana dock, {time}" : $"Ana dock, {time}, {detail}");
    }

    private void OnMinuteElapsed(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        UpdateClock();
        UpdateMediaVisibility();
        ScheduleNextMinute();
    }

    private void ScheduleNextMinute()
    {
        StopTimer();
        var now = DateTimeOffset.Now;
        var nextMinute = new DateTimeOffset(
            now.Year,
            now.Month,
            now.Day,
            now.Hour,
            now.Minute,
            0,
            now.Offset).AddMinutes(1);

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
