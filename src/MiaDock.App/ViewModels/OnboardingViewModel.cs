using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.App.Models;
using MiaDock.App.Services;
using MiaDock.Core.Settings;
using MiaDock.Core.Theming;
using MiaDock.Modules.Media.Models;
using MiaDock.Modules.Media.ViewModels;
using MiaDock.Platform.Windows.Display;
using MiaDock.Platform.Windows.Startup;
using MiaDock.UI.Services;
using System.Collections.ObjectModel;

namespace MiaDock.App.ViewModels;

public sealed class OnboardingViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsService _settings;
    private readonly MusicModuleViewModel _music;
    private readonly IDisplayTopologyService _displays;
    private readonly IStartupTaskService _startup;
    private readonly IThemeService _theme;
    private readonly IAppLocalizationService _localization;
    private readonly AppearanceSettings _originalAppearance;
    private readonly AppLanguage _originalLanguage;
    private IReadOnlyList<OnboardingStepDefinition> _steps = [];
    private IReadOnlyList<SettingOption<ThemeStyle>> _themes = [];
    private IReadOnlyList<SettingOption<MonitorSelectionMode>> _monitorModes = [];
    private IReadOnlyList<SettingOption<IslandPositionSetting>> _positions = [];
    private IReadOnlyList<SettingOption<IslandInteractionMode>> _interactionModes = [];
    private IReadOnlyList<SettingOption<FullscreenNotificationStyle>> _fullscreenStyles = [];
    private AppLanguage _language;
    private StartupTaskStatus _startupStatus = StartupTaskStatus.Unavailable;
    private int _currentStepIndex;
    private bool _startWithWindows;
    private ThemeStyle _themeStyle;
    private string? _selectedSourceId;
    private MonitorSelectionMode _monitorMode;
    private string? _fixedMonitorId;
    private IslandPositionSetting _position;
    private IslandInteractionMode _interactionMode;
    private bool _fullscreenEnabled;
    private FullscreenNotificationStyle _fullscreenStyle;
    private bool _isStartupTaskAvailable;
    private string _startupStatusMessage = "Başlangıç durumu denetleniyor.";
    private string _validationMessage = string.Empty;
    private bool _isBusy;

    public OnboardingViewModel(
        ISettingsService settings,
        MusicModuleViewModel music,
        IDisplayTopologyService displays,
        IStartupTaskService startup,
        IThemeService theme,
        IAppLocalizationService localization)
    {
        _settings = settings;
        _music = music;
        _displays = displays;
        _startup = startup;
        _theme = theme;
        _localization = localization;
        _originalAppearance = settings.Current.Appearance;
        _originalLanguage = settings.Current.General.Language;
        _language = settings.Current.General.Language;
        var draft = OnboardingDraft.FromSettings(settings.Current);
        _startWithWindows = draft.StartWithWindows;
        _themeStyle = draft.Theme;
        _selectedSourceId = draft.SelectedSourceId;
        _monitorMode = draft.MonitorMode;
        _fixedMonitorId = draft.FixedMonitorId;
        _position = draft.Position;
        _interactionMode = draft.InteractionMode;
        _fullscreenEnabled = draft.FullscreenEnabled;
        _fullscreenStyle = draft.FullscreenStyle;
        RebuildLocalizedContent();
        _music.PropertyChanged += OnMusicPropertyChanged;
        _displays.DisplaysChanged += OnDisplaysChanged;
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public IReadOnlyList<SettingOption<AppLanguage>> Languages { get; } =
    [
        new(AppLanguage.Turkish, "Türkçe"),
        new(AppLanguage.English, "English"),
        new(AppLanguage.Azerbaijani, "Azərbaycan dili"),
        new(AppLanguage.SpanishSpain, "Español (España)"),
        new(AppLanguage.SpanishMexico, "Español (México)"),
        new(AppLanguage.PortugueseBrazil, "Português (Brasil)")
    ];

    public IReadOnlyList<OnboardingStepDefinition> Steps => _steps;
    public IReadOnlyList<SettingOption<ThemeStyle>> Themes => _themes;
    public IReadOnlyList<SettingOption<MonitorSelectionMode>> MonitorModes => _monitorModes;
    public IReadOnlyList<SettingOption<IslandPositionSetting>> Positions => _positions;
    public IReadOnlyList<SettingOption<IslandInteractionMode>> InteractionModes => _interactionModes;
    public IReadOnlyList<SettingOption<FullscreenNotificationStyle>> FullscreenStyles => _fullscreenStyles;

    public OnboardingStep CurrentStep => Steps[CurrentStepIndex].Step;
    public int CurrentStepIndex { get => _currentStepIndex; private set => SetCurrentStep(value); }
    public bool IsFirstStep => CurrentStepIndex == 0;
    public bool IsLastStep => CurrentStepIndex == Steps.Count - 1;
    public string StepPositionText => $"{CurrentStepIndex + 1} / {Steps.Count}";
    public string CurrentStepTitle => Steps[CurrentStepIndex].Title;
    public int ProgressValue => CurrentStepIndex + 1;
    public IReadOnlyList<MediaSourceInfo> MediaSources => _music.Sources;
    public IReadOnlyList<SettingOption<string?>> MediaSourceOptions =>
        [
            new(null, Text("Onboarding.Option.Media.Auto")),
            .. _music.Sources.Select(source => new SettingOption<string?>(source.Id, source.DisplayName))
        ];
    public IReadOnlyList<DisplayDescriptor> Displays => _displays.Displays;
    public ObservableCollection<OnboardingModuleOptionViewModel> ModuleOptions { get; } = [];

    public AppLanguage Language
    {
        get => _language;
        set
        {
            if (!SetProperty(ref _language, value))
            {
                return;
            }

            OnPropertyChanged(nameof(LanguageIndex));
            if (_localization.CurrentLanguage != value)
            {
                _localization.SetLanguage(value);
            }
            else
            {
                RebuildLocalizedContent();
            }
        }
    }

    public int LanguageIndex
    {
        get => IndexOf(Languages, Language);
        set => Language = ValueAt(Languages, value, Language);
    }

    public int ThemeIndex { get => IndexOf(Themes, Theme); set => Theme = ValueAt(Themes, value, Theme); }
    public int MonitorModeIndex { get => IndexOf(MonitorModes, MonitorMode); set => MonitorMode = ValueAt(MonitorModes, value, MonitorMode); }
    public int PositionIndex { get => IndexOf(Positions, Position); set => Position = ValueAt(Positions, value, Position); }
    public int InteractionModeIndex { get => IndexOf(InteractionModes, InteractionMode); set => InteractionMode = ValueAt(InteractionModes, value, InteractionMode); }
    public int FullscreenStyleIndex { get => IndexOf(FullscreenStyles, FullscreenStyle); set => FullscreenStyle = ValueAt(FullscreenStyles, value, FullscreenStyle); }
    public int MediaSourceIndex { get => IndexOf(MediaSourceOptions, SelectedSourceId); set => SelectedSourceId = ValueAt(MediaSourceOptions, value, SelectedSourceId); }
    public int FixedMonitorIndex
    {
        get
        {
            for (var index = 0; index < Displays.Count; index++)
            {
                if (Displays[index].Id == FixedMonitorId) return index;
            }
            return -1;
        }
        set => FixedMonitorId = value >= 0 && value < Displays.Count ? Displays[value].Id : null;
    }

    public bool StartWithWindows { get => _startWithWindows; set => SetProperty(ref _startWithWindows, value); }
    public ThemeStyle Theme
    {
        get => _themeStyle;
        set
        {
            if (SetProperty(ref _themeStyle, value))
            {
                _theme.Apply(_settings.Current.Appearance with { Theme = value });
                OnPropertyChanged(nameof(ThemeIndex));
                NotifySummary();
            }
        }
    }
    public string? SelectedSourceId { get => _selectedSourceId; set { if (SetProperty(ref _selectedSourceId, value)) { OnPropertyChanged(nameof(MediaSourceIndex)); NotifySummary(); } } }
    public MonitorSelectionMode MonitorMode { get => _monitorMode; set { if (SetProperty(ref _monitorMode, value)) { OnPropertyChanged(nameof(MonitorModeIndex)); NotifySummary(); } } }
    public string? FixedMonitorId { get => _fixedMonitorId; set { if (SetProperty(ref _fixedMonitorId, value)) { OnPropertyChanged(nameof(FixedMonitorIndex)); NotifySummary(); } } }
    public IslandPositionSetting Position { get => _position; set { if (SetProperty(ref _position, value)) { OnPropertyChanged(nameof(PositionIndex)); NotifySummary(); } } }
    public IslandInteractionMode InteractionMode { get => _interactionMode; set { if (SetProperty(ref _interactionMode, value)) { OnPropertyChanged(nameof(InteractionModeIndex)); NotifySummary(); } } }
    public bool FullscreenEnabled { get => _fullscreenEnabled; set { if (SetProperty(ref _fullscreenEnabled, value)) NotifySummary(); } }
    public FullscreenNotificationStyle FullscreenStyle { get => _fullscreenStyle; set { if (SetProperty(ref _fullscreenStyle, value)) { OnPropertyChanged(nameof(FullscreenStyleIndex)); NotifySummary(); } } }
    public bool IsStartupTaskAvailable { get => _isStartupTaskAvailable; private set => SetProperty(ref _isStartupTaskAvailable, value); }
    public string StartupStatusMessage { get => _startupStatusMessage; private set => SetProperty(ref _startupStatusMessage, value); }
    public string ValidationMessage { get => _validationMessage; private set => SetProperty(ref _validationMessage, value); }
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    public string SummaryText =>
        Text("Onboarding.Summary.Theme", Label(Themes, Theme)) + "\n" +
        Text(
            "Onboarding.Summary.Media",
            MediaSources.FirstOrDefault(source => source.Id == SelectedSourceId)?.DisplayName ??
            Text("Onboarding.Option.Media.Auto")) + "\n" +
        Text("Onboarding.Summary.Monitor", Label(MonitorModes, MonitorMode)) + "\n" +
        Text("Onboarding.Summary.Position", Label(Positions, Position)) + "\n" +
        Text("Onboarding.Summary.Interaction", Label(InteractionModes, InteractionMode)) + "\n" +
        Text(
            "Onboarding.Summary.Fullscreen",
            FullscreenEnabled
                ? Label(FullscreenStyles, FullscreenStyle)
                : Text("Common.Disabled")) + "\n" +
        Text(
            "Onboarding.Summary.Modules",
            string.Join(", ", ModuleOptions.Where(option => option.IsSelected).Select(option => option.Title)));

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var status = await _startup.GetStatusAsync(cancellationToken);
        _startupStatus = status;
        IsStartupTaskAvailable = status != StartupTaskStatus.Unavailable;
        StartWithWindows = status is StartupTaskStatus.Enabled or StartupTaskStatus.EnabledByPolicy;
        UpdateStartupStatusMessage();
    }

    public bool MoveNext()
    {
        if (!ValidateCurrentStep() || IsLastStep)
        {
            return false;
        }

        CurrentStepIndex++;
        return true;
    }

    public bool MoveBack()
    {
        if (IsFirstStep)
        {
            return false;
        }

        ValidationMessage = string.Empty;
        CurrentStepIndex--;
        return true;
    }

    public async Task<bool> CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (!IsLastStep || IsBusy)
        {
            return false;
        }

        IsBusy = true;
        try
        {
            var startupStatus = IsStartupTaskAvailable
                ? await _startup.SetEnabledAsync(StartWithWindows, cancellationToken)
                : StartupTaskStatus.Unavailable;
            var startupEnabled = startupStatus is StartupTaskStatus.Enabled or StartupTaskStatus.EnabledByPolicy;
            _settings.Update(settings => settings with
            {
                Appearance = settings.Appearance with { Theme = Theme },
                Media = settings.Media with { SelectedSourceId = SelectedSourceId },
                Monitor = new MonitorSettings(MonitorMode, MonitorMode == MonitorSelectionMode.Fixed ? FixedMonitorId : null),
                General = settings.General with
                {
                    Language = Language,
                    Position = Position,
                    InteractionMode = InteractionMode
                },
                Fullscreen = settings.Fullscreen with { Enabled = FullscreenEnabled, Style = FullscreenStyle },
                Modules = ApplyModuleSelection(settings.Modules),
                StartupShutdown = settings.StartupShutdown with { StartWithWindows = startupEnabled },
                Onboarding = new OnboardingSettings(true, OnboardingSettings.CurrentVersion, DateTimeOffset.UtcNow)
            });
            await _settings.FlushAsync(cancellationToken);
            return true;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void RestorePreviewTheme()
    {
        _theme.Apply(_originalAppearance);
        if (_localization.CurrentLanguage != _originalLanguage)
        {
            _localization.SetLanguage(_originalLanguage);
        }
    }

    public void Dispose()
    {
        _music.PropertyChanged -= OnMusicPropertyChanged;
        _displays.DisplaysChanged -= OnDisplaysChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
        foreach (var option in ModuleOptions) option.PropertyChanged -= OnModuleOptionChanged;
    }

    private bool ValidateCurrentStep()
    {
        ValidationMessage = string.Empty;
        if (CurrentStep == OnboardingStep.Personalization && MonitorMode == MonitorSelectionMode.Fixed)
        {
            if (string.IsNullOrWhiteSpace(FixedMonitorId) || _displays.Find(FixedMonitorId) is null)
            {
                ValidationMessage = Text("Onboarding.Validation.FixedMonitor");
                return false;
            }
        }

        return true;
    }

    private void SetCurrentStep(int value)
    {
        var normalized = Math.Clamp(value, 0, Steps.Count - 1);
        if (!SetProperty(ref _currentStepIndex, normalized))
        {
            return;
        }

        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(IsFirstStep));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(StepPositionText));
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(ProgressValue));
    }

    private void OnMusicPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MusicModuleViewModel.Sources))
        {
            OnPropertyChanged(nameof(MediaSources));
            OnPropertyChanged(nameof(MediaSourceOptions));
            OnPropertyChanged(nameof(MediaSourceIndex));
            NotifySummary();
        }
    }

    private void OnDisplaysChanged(object? sender, IReadOnlyList<DisplayDescriptor> displays)
    {
        if (MonitorMode == MonitorSelectionMode.Fixed && displays.All(display => display.Id != FixedMonitorId))
        {
            MonitorMode = MonitorSelectionMode.Primary;
            FixedMonitorId = null;
        }

        OnPropertyChanged(nameof(Displays));
        OnPropertyChanged(nameof(FixedMonitorIndex));
    }

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        if (_language != _localization.CurrentLanguage)
        {
            SetProperty(ref _language, _localization.CurrentLanguage, nameof(Language));
            OnPropertyChanged(nameof(LanguageIndex));
        }

        RebuildLocalizedContent();
    }

    private void RebuildLocalizedContent()
    {
        _steps =
        [
            new(OnboardingStep.Welcome, Text("Onboarding.Step.Welcome")),
            new(OnboardingStep.Personalization, Text("Onboarding.Step.Personalization")),
            new(OnboardingStep.Interaction, Text("Onboarding.Step.Interaction")),
            new(OnboardingStep.FeaturesAndPrivacy, Text("Onboarding.Step.FeaturesAndPrivacy")),
            new(OnboardingStep.Ready, Text("Onboarding.Step.Ready"))
        ];
        _themes =
        [
            new(ThemeStyle.AppleLike, Text("Onboarding.Option.Theme.Apple")),
            new(ThemeStyle.OledBlack, "OLED Black"),
            new(ThemeStyle.Windows11Mica, "Windows 11 Mica"),
            new(ThemeStyle.Windows11MicaAlt, "Windows 11 Mica Alt"),
            new(ThemeStyle.Windows11Acrylic, "Windows 11 Acrylic"),
            new(ThemeStyle.Windows11AcrylicThin, "Windows 11 Acrylic Thin"),
            new(ThemeStyle.BlurredGlass, Text("Onboarding.Option.Theme.Glass")),
            new(ThemeStyle.NeutralFrostedGlass, Text("Theme.NeutralFrostedGlass")),
            new(ThemeStyle.AdaptiveFluent, "Adaptive Fluent"),
            new(ThemeStyle.CustomSolidColor, Text("Onboarding.Option.Theme.Solid"))
        ];
        _monitorModes =
        [
            new(MonitorSelectionMode.Primary, Text("Onboarding.Option.Monitor.Primary")),
            new(MonitorSelectionMode.ActiveWindow, Text("Onboarding.Option.Monitor.Active")),
            new(MonitorSelectionMode.Fixed, Text("Onboarding.Option.Monitor.Fixed"))
        ];
        _positions =
        [
            new(IslandPositionSetting.TopCenter, Text("Onboarding.Option.Position.TopCenter")),
            new(IslandPositionSetting.TopLeft, Text("Onboarding.Option.Position.TopLeft")),
            new(IslandPositionSetting.TopRight, Text("Onboarding.Option.Position.TopRight")),
            new(IslandPositionSetting.BottomCenter, Text("Onboarding.Option.Position.BottomCenter")),
            new(IslandPositionSetting.BottomLeft, Text("Onboarding.Option.Position.BottomLeft")),
            new(IslandPositionSetting.BottomRight, Text("Onboarding.Option.Position.BottomRight"))
        ];
        _interactionModes =
        [
            new(IslandInteractionMode.Hover, Text("Onboarding.Option.Interaction.Hover")),
            new(IslandInteractionMode.Click, Text("Onboarding.Option.Interaction.Click")),
            new(IslandInteractionMode.HoverAndClick, Text("Onboarding.Option.Interaction.Both"))
        ];
        _fullscreenStyles =
        [
            new(FullscreenNotificationStyle.Minimal, Text("Onboarding.Option.Fullscreen.Minimal")),
            new(FullscreenNotificationStyle.WithControls, Text("Onboarding.Option.Fullscreen.Controls"))
        ];

        RebuildModuleOptions();
        OnPropertyChanged(nameof(Steps));
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(Themes));
        OnPropertyChanged(nameof(ThemeIndex));
        OnPropertyChanged(nameof(MonitorModes));
        OnPropertyChanged(nameof(MonitorModeIndex));
        OnPropertyChanged(nameof(Positions));
        OnPropertyChanged(nameof(PositionIndex));
        OnPropertyChanged(nameof(InteractionModes));
        OnPropertyChanged(nameof(InteractionModeIndex));
        OnPropertyChanged(nameof(FullscreenStyles));
        OnPropertyChanged(nameof(FullscreenStyleIndex));
        OnPropertyChanged(nameof(MediaSourceOptions));
        OnPropertyChanged(nameof(MediaSourceIndex));
        UpdateStartupStatusMessage();
        NotifySummary();
    }

    private void RebuildModuleOptions()
    {
        var selected = ModuleOptions.ToDictionary(
            option => option.ModuleId,
            option => option.IsSelected,
            StringComparer.Ordinal);
        foreach (var option in ModuleOptions)
        {
            option.PropertyChanged -= OnModuleOptionChanged;
        }
        ModuleOptions.Clear();

        foreach (var option in CreateModuleOptions(_settings.Current, selected))
        {
            ModuleOptions.Add(option);
            option.PropertyChanged += OnModuleOptionChanged;
        }
    }

    private void UpdateStartupStatusMessage()
    {
        StartupStatusMessage = _startupStatus switch
        {
            StartupTaskStatus.Unavailable => Text("Onboarding.Startup.Unavailable"),
            StartupTaskStatus.Failed => Text("Onboarding.Startup.Failed"),
            StartupTaskStatus.DisabledByUser => Text("Onboarding.Startup.DisabledByUser"),
            StartupTaskStatus.DisabledByPolicy => Text("Onboarding.Startup.DisabledByPolicy"),
            StartupTaskStatus.EnabledByPolicy => Text("Onboarding.Startup.EnabledByPolicy"),
            StartupTaskStatus.Enabled => Text("Onboarding.Startup.Enabled"),
            _ => Text("Onboarding.Startup.Disabled")
        };
    }

    private void NotifySummary() => OnPropertyChanged(nameof(SummaryText));

    private void OnModuleOptionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(OnboardingModuleOptionViewModel.IsSelected)) NotifySummary();
    }

    private IReadOnlyDictionary<string, ModuleSettingsEnvelope> ApplyModuleSelection(
        IReadOnlyDictionary<string, ModuleSettingsEnvelope> current)
    {
        var modules = new Dictionary<string, ModuleSettingsEnvelope>(current, StringComparer.Ordinal);
        foreach (var option in ModuleOptions.Where(option => option.CanSelectDuringOnboarding))
        {
            if (modules.TryGetValue(option.ModuleId, out var envelope))
            {
                modules[option.ModuleId] = envelope with { IsEnabled = option.IsSelected };
            }
        }
        return modules;
    }

    private IEnumerable<OnboardingModuleOptionViewModel> CreateModuleOptions(
        MiaDockSettings settings,
        IReadOnlyDictionary<string, bool>? selectedOverrides = null)
    {
        bool Enabled(string id) => selectedOverrides?.TryGetValue(id, out var selected) == true
            ? selected
            : settings.Modules.TryGetValue(id, out var envelope) && envelope.IsEnabled;
        yield return new("media", Text("Onboarding.Module.Media.Title"), Text("Onboarding.Module.Media.Description"), "\uE8D6", Enabled("media"));
        yield return new("volume", Text("Onboarding.Module.Volume.Title"), Text("Onboarding.Module.Volume.Description"), "\uE995", Enabled("volume"));
        yield return new("privacy", Text("Onboarding.Module.Privacy.Title"), Text("Onboarding.Module.Privacy.Description"), "\uE72E", Enabled("privacy"));
        yield return new("system-activity", Text("Onboarding.Module.System.Title"), Text("Onboarding.Module.System.Description"), "\uE717", Enabled("system-activity"));
        yield return new("battery", Text("Onboarding.Module.Battery.Title"), Text("Onboarding.Module.Battery.Description"), "\uE850", Enabled("battery"));
        yield return new("network", Text("Onboarding.Module.Network.Title"), Text("Onboarding.Module.Network.Description"), "\uE968", Enabled("network"));
        yield return new("bluetooth", Text("Onboarding.Module.Bluetooth.Title"), Text("Onboarding.Module.Bluetooth.Description"), "\uE702", Enabled("bluetooth"));
        yield return new("timer", Text("Onboarding.Module.Timer.Title"), Text("Onboarding.Module.Timer.Description"), "\uE823", Enabled("timer"));
        yield return new("transfers", Text("Onboarding.Module.Transfers.Title"), Text("Onboarding.Module.Transfers.Description"), "\uE898", Enabled("transfers"));
        yield return new(
            "notifications",
            Text("Onboarding.Module.Notifications.Title"),
            Text("Onboarding.Module.Notifications.Description"),
            "\uEA8F",
            false,
            canSelectDuringOnboarding: false);
    }

    private string Text(string key, params object?[] arguments) =>
        _localization.Get(key, arguments);

    private static string Label<T>(IReadOnlyList<SettingOption<T>> options, T value) =>
        options.FirstOrDefault(option => EqualityComparer<T>.Default.Equals(option.Value, value))?.Label ?? value?.ToString() ?? string.Empty;

    private static int IndexOf<T>(IReadOnlyList<SettingOption<T>> options, T value)
    {
        for (var index = 0; index < options.Count; index++)
        {
            if (EqualityComparer<T>.Default.Equals(options[index].Value, value)) return index;
        }
        return -1;
    }

    private static T ValueAt<T>(IReadOnlyList<SettingOption<T>> options, int index, T fallback) =>
        index >= 0 && index < options.Count ? options[index].Value : fallback;
}
