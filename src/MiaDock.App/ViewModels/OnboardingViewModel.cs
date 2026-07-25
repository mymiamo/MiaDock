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
    private readonly AppearanceSettings _originalAppearance;
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
        IThemeService theme)
    {
        _settings = settings;
        _music = music;
        _displays = displays;
        _startup = startup;
        _theme = theme;
        _originalAppearance = settings.Current.Appearance;
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
        foreach (var option in CreateModuleOptions(settings.Current))
        {
            ModuleOptions.Add(option);
            option.PropertyChanged += OnModuleOptionChanged;
        }
        _music.PropertyChanged += OnMusicPropertyChanged;
        _displays.DisplaysChanged += OnDisplaysChanged;
    }

    public IReadOnlyList<OnboardingStepDefinition> Steps { get; } =
    [
        new(OnboardingStep.Welcome, "Hoş geldiniz"),
        new(OnboardingStep.Startup, "Windows başlangıcı"),
        new(OnboardingStep.Appearance, "Tema"),
        new(OnboardingStep.Media, "Medya"),
        new(OnboardingStep.Display, "Monitör ve konum"),
        new(OnboardingStep.Interaction, "Etkileşim"),
        new(OnboardingStep.Fullscreen, "Tam ekran"),
        new(OnboardingStep.Modules, "Modüller"),
        new(OnboardingStep.Summary, "Özet")
    ];

    public IReadOnlyList<SettingOption<ThemeStyle>> Themes { get; } =
    [
        new(ThemeStyle.AppleLike, "Apple benzeri"),
        new(ThemeStyle.Windows11Mica, "Windows 11 Mica"),
        new(ThemeStyle.Windows11MicaAlt, "Windows 11 Mica Alt"),
        new(ThemeStyle.Windows11Acrylic, "Windows 11 Acrylic"),
        new(ThemeStyle.Windows11AcrylicThin, "Windows 11 Acrylic Thin"),
        new(ThemeStyle.BlurredGlass, "Bulanık Cam"),
        new(ThemeStyle.CustomSolidColor, "Özel Düz Renk")
    ];
    public IReadOnlyList<SettingOption<MonitorSelectionMode>> MonitorModes { get; } =
        [new(MonitorSelectionMode.Primary, "Ana monitör"), new(MonitorSelectionMode.ActiveWindow, "Aktif pencerenin monitörü"), new(MonitorSelectionMode.Fixed, "Sabit monitör")];
    public IReadOnlyList<SettingOption<IslandPositionSetting>> Positions { get; } =
        [new(IslandPositionSetting.TopCenter, "Üst orta"), new(IslandPositionSetting.TopLeft, "Üst sol"), new(IslandPositionSetting.TopRight, "Üst sağ"), new(IslandPositionSetting.BottomCenter, "Alt orta"), new(IslandPositionSetting.BottomLeft, "Alt sol"), new(IslandPositionSetting.BottomRight, "Alt sağ")];
    public IReadOnlyList<SettingOption<IslandInteractionMode>> InteractionModes { get; } =
        [new(IslandInteractionMode.Hover, "Fare üzerine gelince"), new(IslandInteractionMode.Click, "Tıklayınca"), new(IslandInteractionMode.HoverAndClick, "Fare ve tıklama")];
    public IReadOnlyList<SettingOption<FullscreenNotificationStyle>> FullscreenStyles { get; } =
        [new(FullscreenNotificationStyle.Minimal, "Sade"), new(FullscreenNotificationStyle.WithControls, "Kontrollü")];

    public OnboardingStep CurrentStep => Steps[CurrentStepIndex].Step;
    public int CurrentStepIndex { get => _currentStepIndex; private set => SetCurrentStep(value); }
    public bool IsFirstStep => CurrentStepIndex == 0;
    public bool IsLastStep => CurrentStepIndex == Steps.Count - 1;
    public string StepPositionText => $"{CurrentStepIndex + 1} / {Steps.Count}";
    public IReadOnlyList<MediaSourceInfo> MediaSources => _music.Sources;
    public IReadOnlyList<SettingOption<string?>> MediaSourceOptions =>
        [new(null, "Otomatik seçim"), .. _music.Sources.Select(source => new SettingOption<string?>(source.Id, source.DisplayName))];
    public IReadOnlyList<DisplayDescriptor> Displays => _displays.Displays;
    public ObservableCollection<OnboardingModuleOptionViewModel> ModuleOptions { get; } = [];

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
        $"Tema: {Label(Themes, Theme)}\n" +
        $"Medya: {MediaSources.FirstOrDefault(source => source.Id == SelectedSourceId)?.DisplayName ?? "Otomatik seçim"}\n" +
        $"Monitör: {Label(MonitorModes, MonitorMode)}\n" +
        $"Konum: {Label(Positions, Position)}\n" +
        $"Etkileşim: {Label(InteractionModes, InteractionMode)}\n" +
        $"Tam ekran: {(FullscreenEnabled ? Label(FullscreenStyles, FullscreenStyle) : "Kapalı")}\n" +
        $"Modüller: {string.Join(", ", ModuleOptions.Where(option => option.IsSelected).Select(option => option.Title))}";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var status = await _startup.GetStatusAsync(cancellationToken);
        IsStartupTaskAvailable = status != StartupTaskStatus.Unavailable;
        StartWithWindows = status is StartupTaskStatus.Enabled or StartupTaskStatus.EnabledByPolicy;
        StartupStatusMessage = status switch
        {
            StartupTaskStatus.Unavailable => "Windows ile başlatma, MSIX paketi kurulduğunda kullanılabilir.",
            StartupTaskStatus.DisabledByUser => "Windows bu başlangıç görevini devre dışı bıraktı.",
            StartupTaskStatus.DisabledByPolicy => "Başlangıç görevi sistem ilkesi tarafından engelleniyor.",
            StartupTaskStatus.EnabledByPolicy => "Başlangıç görevi sistem ilkesi tarafından etkinleştirildi.",
            StartupTaskStatus.Enabled => "MiaDock Windows ile başlayacak.",
            _ => "MiaDock Windows ile başlamayacak."
        };
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
                General = settings.General with { Position = Position, InteractionMode = InteractionMode },
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

    public void RestorePreviewTheme() => _theme.Apply(_originalAppearance);

    public void Dispose()
    {
        _music.PropertyChanged -= OnMusicPropertyChanged;
        _displays.DisplaysChanged -= OnDisplaysChanged;
        foreach (var option in ModuleOptions) option.PropertyChanged -= OnModuleOptionChanged;
    }

    private bool ValidateCurrentStep()
    {
        ValidationMessage = string.Empty;
        if (CurrentStep == OnboardingStep.Display && MonitorMode == MonitorSelectionMode.Fixed)
        {
            if (string.IsNullOrWhiteSpace(FixedMonitorId) || _displays.Find(FixedMonitorId) is null)
            {
                ValidationMessage = "Sabit monitör kullanmak için bağlı bir monitör seçin.";
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

    private static IEnumerable<OnboardingModuleOptionViewModel> CreateModuleOptions(MiaDockSettings settings)
    {
        bool Enabled(string id) => settings.Modules.TryGetValue(id, out var envelope) && envelope.IsEnabled;
        yield return new("media", "Medya", "Windows medya oturumları ve oynatma kontrolleri.", "\uE8D6", Enabled("media"));
        yield return new("system-activity", "Ses ve gizlilik göstergeleri", "Ses, mikrofon, kamera durumu ve yerel arama çıkarımı.", "\uE767", Enabled("system-activity"));
        yield return new("battery", "Pil", "Şarj, enerji tasarrufu ve düşük pil olayları.", "\uE850", Enabled("battery"));
        yield return new("network", "Ağ", "Bağlantı türü ve isteğe bağlı hız görünümü.", "\uE968", Enabled("network"));
        yield return new("bluetooth", "Bluetooth", "Eşleştirilmiş cihaz bağlantı değişiklikleri.", "\uE702", Enabled("bluetooth"));
        yield return new("timer", "Zamanlayıcı ve kronometre", "Yerel zaman araçları ve tamamlanma olayları.", "\uE823", Enabled("timer"));
        yield return new("transfers", "Dosya aktarımları", "Yerel sağlayıcılardan gelen aktarım ilerlemesi.", "\uE898", Enabled("transfers"));
        yield return new(
            "notifications",
            "Windows bildirimleri",
            "Kullanıcı izni gerektiği için ilk kurulumdan sonra Modüller sayfasından açılır.",
            "\uEA8F",
            false,
            canSelectDuringOnboarding: false);
    }

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
