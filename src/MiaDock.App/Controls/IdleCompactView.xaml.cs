using System.Globalization;
using System.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MiaDock.App.ViewModels;
using MiaDock.App.Services;
using MiaDock.Core.Localization;
using MiaDock.Core.Presentation;
using MiaDock.Core.Settings;
using MiaDock.Modules.Media.ViewModels;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.ViewModels;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace MiaDock.App.Controls;

public sealed partial class IdleCompactView : UserControl, IModuleViewActivationAware
{
    private DispatcherQueueTimer? _clockTimer;
    private readonly MusicModuleViewModel? _music;
    private readonly SystemActivityViewModel? _systemActivity;
    private readonly PrivacyModuleViewModel? _privacy;
    private readonly ILocalizationService? _localization;
    private readonly ISettingsService? _settings;
    private readonly UISettings _uiSettings = new();
    private readonly Brush _microphoneBrush =
        new SolidColorBrush(Color.FromArgb(255, 74, 222, 128));
    private readonly Brush _cameraBrush =
        new SolidColorBrush(Color.FromArgb(255, 245, 158, 11));
    private bool _isLoaded;
    private bool _musicRefreshPending;
    private bool _isPresentationActive;

    public IdleCompactView() : this(null, null, null, null, null, null)
    {
    }

    public IdleCompactView(MusicModuleViewModel? music) : this(music, null, null, null, null, null)
    {
    }

    public IdleCompactView(
        MusicModuleViewModel? music,
        SystemActivityViewModel? systemActivity,
        ILocalizationService? localization = null,
        ISettingsService? settings = null,
        FocusDockViewModel? focus = null,
        PrivacyModuleViewModel? privacy = null)
    {
        _music = music;
        _systemActivity = systemActivity;
        _privacy = privacy;
        _localization = localization;
        _settings = settings;
        InitializeComponent();
        if (focus is not null)
        {
            FocusStatus.Configure(focus);
        }
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
        }

        if (_systemActivity is not null)
        {
            _systemActivity.PropertyChanged += OnSystemActivityPropertyChanged;
        }
        if (_privacy is not null)
        {
            _privacy.PropertyChanged += OnPrivacyPropertyChanged;
        }
        if (_localization is not null)
        {
            _localization.LanguageChanged += OnLanguageChanged;
        }
        if (_settings is not null)
        {
            _settings.SettingsChanged += OnSettingsChanged;
        }

        UpdateClock();
        UpdateMusicActivity();
        UpdateCallActivity();
        UpdateActivityDot();
        UpdatePresentationActivity();
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
        if (_privacy is not null)
        {
            _privacy.PropertyChanged -= OnPrivacyPropertyChanged;
        }
        if (_localization is not null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }
        if (_settings is not null)
        {
            _settings.SettingsChanged -= OnSettingsChanged;
        }

        IdlePulseStoryboard.Stop();
        StopTimer();
    }

    public void SetPresentationActive(bool isActive)
    {
        _isPresentationActive = isActive;
        UpdatePresentationActivity();
    }

    private void UpdatePresentationActivity()
    {
        var shouldRun = _isLoaded && _isPresentationActive;
        _music?.SetAudioMeterActive(this, shouldRun);
        var hasActivity = _privacy?.HasActiveUsage == true;
        if (shouldRun && hasActivity && _uiSettings.AnimationsEnabled)
        {
            IdlePulseStoryboard.Begin();
        }
        else
        {
            IdlePulseStoryboard.Stop();
        }
    }

    private void OnMusicPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MusicModuleViewModel.Current))
        {
            UpdateMusicActivity();
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
        }
    }

    private void OnPrivacyPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(PrivacyModuleViewModel.State)
            or nameof(PrivacyModuleViewModel.Indicator)
            or nameof(PrivacyModuleViewModel.HasActiveUsage)
            or nameof(PrivacyModuleViewModel.SummaryText))
        {
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
        var indicator = _privacy?.Indicator ?? PrivacyIndicatorKind.Idle;

        switch (indicator)
        {
            case PrivacyIndicatorKind.Camera:
                brush = _cameraBrush;
                status = Text("Privacy_CameraInUse", "Kamera kullanılıyor");
                break;
            case PrivacyIndicatorKind.Microphone:
                brush = _microphoneBrush;
                status = Text("Privacy_MicrophoneInUse", "Mikrofon kullanılıyor");
                break;
            default:
                brush = Application.Current.Resources["IslandStyleAccentBrush"] as Brush
                        ?? new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
                status = Text("Privacy_NoActiveDevices", "Aktif gizlilik kullanımı yok");
                break;
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
        UpdatePresentationActivity();
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
        var display = ClockDisplayFormatter.Format(
            DateTimeOffset.Now,
            CultureInfo.CurrentCulture,
            ClockSettings);
        TimeText.Text = display.Time;
        DateText.Text = display.Date;
        DateContainer.Visibility = ClockSettings.ShowDate
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateAutomationName();
    }

    private void UpdateAutomationName()
    {
        var now = DateTimeOffset.Now;
        var culture = CultureInfo.CurrentCulture;
        var status = (DataContext as IdleDashboardViewModel)?.StatusSummary;
        var display = ClockDisplayFormatter.Format(now, culture, ClockSettings);
        var clock = ClockSettings.ShowDate
            ? Text("Dock.Clock", "Saat {0}, {1}", display.Time, display.Date)
            : Text("Dock.Clock.Short", "Saat {0}", display.Time);
        AutomationProperties.SetName(LayoutRoot,
            string.IsNullOrWhiteSpace(status) ? clock : $"{clock}, {status}");
    }

    private void ScheduleNextMinute()
    {
        StopTimer();

        var now = DateTimeOffset.Now;
        _clockTimer = DispatcherQueue.CreateTimer();
        _clockTimer.IsRepeating = false;
        _clockTimer.Interval = ClockDisplayFormatter.DelayUntilNextRefresh(now, ClockSettings);
        _clockTimer.Tick += OnMinuteElapsed;
        _clockTimer.Start();
    }

    private void StopTimer()
    {
        if (_clockTimer is null)
        {
            return;
        }

        _clockTimer.Stop();
        _clockTimer.Tick -= OnMinuteElapsed;
        _clockTimer = null;
    }

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        UpdateClock();
        UpdateActivityDot();
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        if (args.Previous.General.Clock == args.Current.General.Clock)
        {
            return;
        }

        UpdateClock();
        ScheduleNextMinute();
    }

    private ClockDisplaySettings ClockSettings =>
        _settings?.Current.General.Clock ?? ClockDisplaySettings.Default;

    private string Text(string key, string fallback, params object?[] arguments)
    {
        var value = _localization?.Get(key, arguments);
        return value is not null && value != key
            ? value
            : string.Format(CultureInfo.CurrentCulture, fallback, arguments);
    }
}
