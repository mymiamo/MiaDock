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
using MiaDock.Modules.Media.ViewModels;
using MiaDock.Modules.SystemStatus.ViewModels;

namespace MiaDock.App.Controls;

public sealed partial class IdleExpandedView : UserControl
{
    private readonly MusicModuleViewModel? _music;
    private readonly IdleDashboardViewModel? _dashboard;
    private readonly ILocalizationService? _localization;
    private readonly ISettingsService? _settings;
    private DispatcherQueueTimer? _clockTimer;
    private bool _isLoaded;

    public IdleExpandedView() : this(null, null, null, null, null, null)
    {
    }

    public IdleExpandedView(
        MusicModuleViewModel? music,
        SystemActivityViewModel? system,
        IdleDashboardViewModel? dashboard,
        ILocalizationService? localization = null,
        ISettingsService? settings = null,
        FocusDockViewModel? focus = null)
    {
        _music = music;
        _dashboard = dashboard;
        _localization = localization;
        _settings = settings;
        InitializeComponent();
        LayoutRoot.DataContext = dashboard;
        SystemStatusPanel.DataContext = system;
        MusicPanel.DataContext = music;
        if (focus is not null)
        {
            FocusPanel.Configure(focus);
        }
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
            _music.SetAudioMeterActive(this, true);
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
            _music.SetAudioMeterActive(this, false);
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
        if (args.PropertyName is nameof(MusicModuleViewModel.Current) or
            nameof(MusicModuleViewModel.IsMediaAvailable))
        {
            UpdateMediaVisibility();
        }
    }

    private void UpdateMediaVisibility()
    {
        var hasMedia = _music?.Current.HasMedia == true;
        MediaArtwork.Visibility = hasMedia ? Visibility.Visible : Visibility.Collapsed;
        EmptyArtwork.Visibility = hasMedia ? Visibility.Collapsed : Visibility.Visible;
        MediaMetadataPanel.Visibility = hasMedia ? Visibility.Visible : Visibility.Collapsed;
        MediaEmptyPanel.Visibility = hasMedia ? Visibility.Collapsed : Visibility.Visible;
        UpdateAutomationName(hasMedia);
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
    }

    private void UpdateAutomationName(bool hasMedia)
    {
        var display = ClockDisplayFormatter.Format(
            DateTimeOffset.Now,
            CultureInfo.CurrentCulture,
            ClockSettings);
        var detail = hasMedia
            ? $"{_music!.Current.Track.Title}, {_music.Current.Track.Artist}"
            : _dashboard?.StatusSummary;
        AutomationProperties.SetName(
            LayoutRoot,
            string.IsNullOrWhiteSpace(detail)
                ? Text("Dock.Home.Automation", "Ana dock, {0}", display.Time)
                : $"{Text("Dock.Home.Automation", "Ana dock, {0}", display.Time)}, {detail}");
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
        UpdateMediaVisibility();
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        if (args.Previous.General.Clock == args.Current.General.Clock)
        {
            return;
        }

        UpdateClock();
        UpdateMediaVisibility();
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
