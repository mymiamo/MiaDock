using System.ComponentModel;
using System.Globalization;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.ViewModels;
using MiaDock.App.Services;
using MiaDock.Core.Localization;
using MiaDock.Core.Presentation;
using MiaDock.Core.Settings;
using MiaDock.Modules.Media.Models;
using MiaDock.Modules.Media.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class IdleHoverView : UserControl
{
    private readonly MusicModuleViewModel? _music;
    private readonly IdleDashboardViewModel? _dashboard;
    private readonly ILocalizationService? _localization;
    private readonly ISettingsService? _settings;
    private DispatcherQueueTimer? _clockTimer;

    public IdleHoverView() : this(null, null, null, null)
    {
    }

    public IdleHoverView(
        MusicModuleViewModel? music,
        IdleDashboardViewModel? idleDashboard,
        ILocalizationService? localization = null,
        ISettingsService? settings = null)
    {
        _music = music;
        _dashboard = idleDashboard;
        _localization = localization;
        _settings = settings;
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
        if (_localization is not null)
        {
            _localization.LanguageChanged += OnLanguageChanged;
        }
        if (_settings is not null)
        {
            _settings.SettingsChanged += OnSettingsChanged;
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
        if (_localization is not null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }
        if (_settings is not null)
        {
            _settings.SettingsChanged -= OnSettingsChanged;
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
        var display = ClockDisplayFormatter.Format(
            DateTimeOffset.Now,
            CultureInfo.CurrentCulture,
            ClockSettings);
        TimeText.Text = display.Time;
        DateText.Text = display.Date;
        DateText.Visibility = ClockSettings.ShowDate ? Visibility.Visible : Visibility.Collapsed;
        DateSeparator.Visibility = DateText.Visibility;
    }

    private void UpdateAutomationName(bool showTrack)
    {
        var culture = CultureInfo.CurrentCulture;
        var display = ClockDisplayFormatter.Format(
            DateTimeOffset.Now,
            culture,
            ClockSettings);
        var detail = showTrack
            ? $"{_music!.Current.Track.Title}, {_music.Current.Track.Artist}"
            : _dashboard?.StatusSummary;
        AutomationProperties.SetName(LayoutRoot,
            string.IsNullOrWhiteSpace(detail)
                ? Text("Dock.Clock.Short", "Saat {0}", display.Time)
                : $"{Text("Dock.Clock.Short", "Saat {0}", display.Time)}, {detail}");
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
        UpdateContent();
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        if (args.Previous.General.Clock == args.Current.General.Clock)
        {
            return;
        }

        UpdateClock();
        UpdateContent();
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
