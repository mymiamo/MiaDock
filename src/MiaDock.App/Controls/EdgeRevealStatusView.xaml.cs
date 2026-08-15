using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MiaDock.App.Services;
using MiaDock.App.ViewModels;
using MiaDock.Modules.Media.ViewModels;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.ViewModels;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace MiaDock.App.Controls;

public sealed partial class EdgeRevealStatusView : UserControl, IModuleViewActivationAware
{
    private readonly MusicModuleViewModel? _music;
    private readonly SystemActivityViewModel? _systemActivity;
    private readonly PrivacyModuleViewModel? _privacy;
    private readonly IAppLocalizationService? _localization;
    private readonly UISettings _uiSettings = new();
    private readonly Brush _microphoneBrush =
        new SolidColorBrush(Color.FromArgb(255, 74, 222, 128));
    private readonly Brush _cameraBrush =
        new SolidColorBrush(Color.FromArgb(255, 59, 130, 246));
    private bool _isLoaded;
    private bool _isPresentationActive;

    public EdgeRevealStatusView() : this(null, null, null, null, null)
    {
    }

    public EdgeRevealStatusView(
        MusicModuleViewModel? music,
        SystemActivityViewModel? systemActivity,
        PrivacyModuleViewModel? privacy,
        IAppLocalizationService? localization,
        IdleDashboardViewModel? dashboard)
    {
        _music = music;
        _systemActivity = systemActivity;
        _privacy = privacy;
        _localization = localization;
        InitializeComponent();
        DataContext = dashboard;
    }

    public void SetPresentationActive(bool isActive)
    {
        _isPresentationActive = isActive;
        UpdatePresentationActivity();
        UpdateMusicActivity();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
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

        UpdateActivityDot();
        UpdateCallActivity();
        UpdateMusicActivity();
        UpdateAutomationName();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
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

        ActivityPulseStoryboard.Stop();
    }

    private void OnDashboardPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(IdleDashboardViewModel.StatusSummary))
        {
            UpdateAutomationName();
        }
    }

    private void OnMusicPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(MusicModuleViewModel.Current)
            or nameof(MusicModuleViewModel.LeftAudioLevel)
            or nameof(MusicModuleViewModel.CenterAudioLevel)
            or nameof(MusicModuleViewModel.RightAudioLevel)
            or nameof(MusicModuleViewModel.IsAudioLevelAvailable)
            or nameof(MusicModuleViewModel.HasAudioActivity))
        {
            UpdateMusicActivity();
        }
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
        if (args.PropertyName is nameof(PrivacyModuleViewModel.Indicator)
            or nameof(PrivacyModuleViewModel.HasActiveUsage)
            or nameof(PrivacyModuleViewModel.SummaryText))
        {
            UpdateActivityDot();
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        UpdateActivityDot();
        UpdateAutomationName();
    }

    private void UpdateActivityDot()
    {
        Brush brush;
        string status;
        switch (_privacy?.Indicator ?? PrivacyIndicatorKind.Idle)
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

        ActivityPulse.Fill = brush;
        ActivityDot.Fill = brush;
        ToolTipService.SetToolTip(ActivityDot, status);
        AutomationProperties.SetName(ActivityDot, status);
        UpdatePresentationActivity();
    }

    private void UpdateCallActivity()
    {
        var callDetected = _systemActivity?.Snapshot.CallActivity == CallActivityState.Possible;
        CallActivityIcon.Visibility = callDetected ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateMusicActivity()
    {
        if (_music is null)
        {
            MusicActivity.Visibility = Visibility.Collapsed;
            return;
        }

        _music.SetAudioMeterActive(this, _isLoaded && _isPresentationActive);
        MusicActivity.Visibility = _music.HasAudioActivity ? Visibility.Visible : Visibility.Collapsed;
        MusicActivity.IsPlaying = _music.HasAudioActivity;
        MusicActivity.IsAudioAvailable = _music.IsAudioLevelAvailable;
        MusicActivity.LeftLevel = _music.LeftAudioLevel;
        MusicActivity.CenterLevel = _music.CenterAudioLevel;
        MusicActivity.RightLevel = _music.RightAudioLevel;
    }

    private void UpdatePresentationActivity()
    {
        var shouldPulse = _isLoaded &&
                          _isPresentationActive &&
                          _privacy?.HasActiveUsage == true &&
                          _uiSettings.AnimationsEnabled;
        if (shouldPulse)
        {
            ActivityPulseStoryboard.Begin();
        }
        else
        {
            ActivityPulseStoryboard.Stop();
        }
    }

    private void UpdateAutomationName()
    {
        var status = (DataContext as IdleDashboardViewModel)?.StatusSummary;
        var title = _localization?.Text("Kenarda gizli dock", "Dock hidden at edge")
                    ?? "Kenarda gizli dock";
        AutomationProperties.SetName(
            LayoutRoot,
            string.IsNullOrWhiteSpace(status) ? title : $"{title}, {status}");
    }

    private string Text(string key, string fallback)
    {
        var value = _localization?.Get(key);
        return value is not null && value != key ? value : fallback;
    }
}
