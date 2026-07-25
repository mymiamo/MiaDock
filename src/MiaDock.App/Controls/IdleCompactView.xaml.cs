using System.Globalization;
using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MiaDock.App.ViewModels;
using MiaDock.Modules.Media.ViewModels;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.ViewModels;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace MiaDock.App.Controls;

public sealed partial class IdleCompactView : UserControl
{
    private DispatcherQueueTimer? _minuteTimer;
    private readonly MusicModuleViewModel? _music;
    private readonly SystemActivityViewModel? _systemActivity;
    private readonly UISettings _uiSettings = new();
    private readonly Brush _microphoneBrush =
        new SolidColorBrush(Color.FromArgb(255, 245, 158, 11));
    private readonly Brush _speakerBrush =
        new SolidColorBrush(Color.FromArgb(255, 74, 222, 128));
    private bool _isLoaded;
    private bool _musicRefreshPending;

    public IdleCompactView() : this(null, null)
    {
    }

    public IdleCompactView(MusicModuleViewModel? music) : this(music, null)
    {
    }

    public IdleCompactView(
        MusicModuleViewModel? music,
        SystemActivityViewModel? systemActivity)
    {
        _music = music;
        _systemActivity = systemActivity;
        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        if (DataContext is IdleDashboardViewModel dashboard)
        {
            dashboard.PropertyChanged += OnDashboardPropertyChanged;
        }

        if (_music is not null)
        {
            _music.PropertyChanged += OnMusicPropertyChanged;
            _music.SetAudioMeterActive(this, true);
        }

        if (_systemActivity is not null)
        {
            _systemActivity.PropertyChanged += OnSystemActivityPropertyChanged;
        }

        UpdateClock();
        UpdateMusicActivity();
        UpdateCallActivity();
        UpdateActivityDot();
        if (_uiSettings.AnimationsEnabled)
        {
            IdlePulseStoryboard.Begin();
        }
        ScheduleNextMinute();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        _isLoaded = false;
        if (DataContext is IdleDashboardViewModel dashboard)
        {
            dashboard.PropertyChanged -= OnDashboardPropertyChanged;
        }

        if (_music is not null)
        {
            _music.PropertyChanged -= OnMusicPropertyChanged;
            _music.SetAudioMeterActive(this, false);
        }

        if (_systemActivity is not null)
        {
            _systemActivity.PropertyChanged -= OnSystemActivityPropertyChanged;
        }

        IdlePulseStoryboard.Stop();
        StopTimer();
    }

    private void OnMusicPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MusicModuleViewModel.Current))
        {
            UpdateMusicActivity();
            UpdateActivityDot();
            return;
        }

        if (args.PropertyName is nameof(MusicModuleViewModel.LeftAudioLevel)
            or nameof(MusicModuleViewModel.CenterAudioLevel)
            or nameof(MusicModuleViewModel.RightAudioLevel)
            or nameof(MusicModuleViewModel.IsAudioLevelAvailable)
            or nameof(MusicModuleViewModel.HasAudioActivity))
        {
            QueueMusicVisualRefresh();
        }
    }

    private void QueueMusicVisualRefresh()
    {
        if (!_isLoaded || _musicRefreshPending)
        {
            return;
        }

        _musicRefreshPending = true;
        if (!DispatcherQueue.TryEnqueue(() =>
            {
                _musicRefreshPending = false;
                if (!_isLoaded)
                {
                    return;
                }

                UpdateMusicActivity();
                UpdateActivityDot();
            }))
        {
            _musicRefreshPending = false;
        }
    }

    private void UpdateMusicActivity()
    {
        if (_music is null)
        {
            MusicActivity.Visibility = Visibility.Collapsed;
            return;
        }

        MusicActivity.Visibility = _music.HasAudioActivity ? Visibility.Visible : Visibility.Collapsed;
        MusicActivity.IsPlaying = _music.HasAudioActivity;
        MusicActivity.IsAudioAvailable = _music.IsAudioLevelAvailable;
        MusicActivity.LeftLevel = _music.LeftAudioLevel;
        MusicActivity.CenterLevel = _music.CenterAudioLevel;
        MusicActivity.RightLevel = _music.RightAudioLevel;
    }

    private void OnSystemActivityPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(SystemActivityViewModel.Snapshot))
        {
            UpdateCallActivity();
            UpdateActivityDot();
        }
    }

    private void UpdateCallActivity()
    {
        var callDetected = _systemActivity?.Snapshot.CallActivity == CallActivityState.Possible;
        CallActivityIcon.Visibility = callDetected ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateActivityDot()
    {
        Brush brush;
        string status;

        if (_systemActivity?.Snapshot.MicrophoneUsage == MicrophoneUsageState.Active)
        {
            brush = _microphoneBrush;
            status = "Mikrofon kullanılıyor";
        }
        else if (_music?.HasAudioActivity == true)
        {
            brush = _speakerBrush;
            status = "Hoparlör kullanılıyor";
        }
        else
        {
            brush = Application.Current.Resources["IslandStyleAccentBrush"] as Brush
                    ?? new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
            status = "Ses etkinliği yok";
        }

        if (!ReferenceEquals(IdlePulse.Fill, brush))
        {
            IdlePulse.Fill = brush;
        }

        if (!ReferenceEquals(ActivityDot.Fill, brush))
        {
            ActivityDot.Fill = brush;
        }
        ToolTipService.SetToolTip(ActivityDot, status);
        AutomationProperties.SetName(ActivityDot, status);
    }

    private void OnDashboardPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IdleDashboardViewModel.StatusSummary))
        {
            UpdateAutomationName();
        }
    }

    private void OnMinuteElapsed(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        UpdateClock();
        ScheduleNextMinute();
    }

    private void UpdateClock()
    {
        var now = DateTimeOffset.Now;
        var culture = CultureInfo.CurrentCulture;
        TimeText.Text = now.ToString("HH:mm", culture);
        DateText.Text = now.ToString("ddd, d MMM", culture);
        UpdateAutomationName();
    }

    private void UpdateAutomationName()
    {
        var now = DateTimeOffset.Now;
        var culture = CultureInfo.CurrentCulture;
        var status = (DataContext as IdleDashboardViewModel)?.StatusSummary;
        var clock = string.Format(culture, "Saat {0}, {1}",
            now.ToString("HH:mm", culture),
            now.ToString("D", culture));
        AutomationProperties.SetName(LayoutRoot,
            string.IsNullOrWhiteSpace(status) ? clock : $"{clock}, {status}");
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
