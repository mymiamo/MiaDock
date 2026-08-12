using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiaDock.Core.Localization;
using MiaDock.Modules.SystemStatus.Models;
using MiaDock.Modules.SystemStatus.Services;

namespace MiaDock.Modules.SystemStatus.ViewModels;

public sealed partial class PrivacyModuleViewModel : ObservableObject, IDisposable
{
    private readonly IPrivacyUsageService _service;
    private readonly ILocalizationService? _localization;
    private readonly IPrivacySettingsLauncher? _privacySettingsLauncher;

    public PrivacyModuleViewModel(
        IPrivacyUsageService service,
        ILocalizationService? localization = null,
        IPrivacySettingsLauncher? privacySettingsLauncher = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _localization = localization;
        _privacySettingsLauncher = privacySettingsLauncher;
        State = service.Current;
        Applications = CreateItems(State);
        service.StateChanged += OnStateChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged += OnLanguageChanged;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveUsage))]
    [NotifyPropertyChangedFor(nameof(TitleText))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(EmptyText))]
    [NotifyPropertyChangedFor(nameof(Indicator))]
    [NotifyPropertyChangedFor(nameof(StatusGlyph))]
    public partial PrivacyState State { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<PrivacyApplicationItemViewModel> Applications { get; set; } =
        Array.Empty<PrivacyApplicationItemViewModel>();

    public bool HasActiveUsage => State.MicrophoneInUse || State.CameraInUse;

    public PrivacyIndicatorKind Indicator => State.Indicator;

    public string TitleText => Text("Privacy_Title", "Gizlilik");

    public string SummaryText =>
        State.CameraInUse && State.MicrophoneInUse
            ? Text("Privacy_CameraAndMicrophoneInUse", "Kamera ve mikrofon kullanılıyor")
            : State.CameraInUse
                ? Text("Privacy_CameraInUse", "Kamera kullanılıyor")
                : State.MicrophoneInUse
                    ? Text("Privacy_MicrophoneInUse", "Mikrofon kullanılıyor")
                    : Text("Privacy_NoActiveDevices", "Aktif gizlilik kullanımı yok");

    public string EmptyText => Text("Privacy_NoActiveDevices", "Aktif gizlilik kullanımı yok");

    public string ActiveApplicationsHeader =>
        Text("Privacy_ActiveApplications", "Aktif uygulamalar");

    public string StatusGlyph => State.CameraInUse
        ? "\uE714"
        : State.MicrophoneInUse
            ? "\uE720"
            : "\uE72E";

    [RelayCommand]
    private Task OpenMicrophonePrivacySettingsAsync() =>
        _privacySettingsLauncher?.OpenMicrophonePrivacySettingsAsync() ?? Task.FromResult(false);

    [RelayCommand]
    private Task OpenCameraPrivacySettingsAsync() =>
        _privacySettingsLauncher?.OpenCameraPrivacySettingsAsync() ?? Task.FromResult(false);

    private void OnStateChanged(object? sender, PrivacyState state) => Apply(state);

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        OnPropertyChanged(nameof(TitleText));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(ActiveApplicationsHeader));
        Applications = CreateItems(State);
    }

    private void Apply(PrivacyState state)
    {
        State = state;
        Applications = CreateItems(state);
    }

    private IReadOnlyList<PrivacyApplicationItemViewModel> CreateItems(PrivacyState state) =>
        state.ActiveApplications
            .Select(app => new PrivacyApplicationItemViewModel(app, DescribeUsage(app), _localization))
            .ToArray();

    private string DescribeUsage(PrivacyApplication app)
    {
        if (app.UsesBoth)
        {
            return Text("Privacy_CameraAndMicrophoneInUse", "Kamera ve mikrofon kullanılıyor");
        }

        if (app.UsesCamera)
        {
            return Text("Privacy_CameraInUse", "Kamera kullanılıyor");
        }

        return Text("Privacy_MicrophoneInUse", "Mikrofon kullanılıyor");
    }

    private string Text(string key, string fallback) =>
        _localization?.Get(key) is { } value && value != key ? value : fallback;

    public void Dispose()
    {
        _service.StateChanged -= OnStateChanged;
        if (_localization is not null)
        {
            _localization.LanguageChanged -= OnLanguageChanged;
        }
    }
}

public sealed partial class PrivacyApplicationItemViewModel : ObservableObject
{
    public PrivacyApplicationItemViewModel(
        PrivacyApplication application,
        string usageText,
        ILocalizationService? localization)
    {
        Application = application;
        UsageText = usageText;
        _ = localization;
    }

    public PrivacyApplication Application { get; }

    public string DisplayName => Application.DisplayName;

    public string UsageText { get; }

    public string? ExecutablePath => Application.ExecutablePath;

    public bool UsesMicrophone => Application.UsesMicrophone;

    public bool UsesCamera => Application.UsesCamera;

    public string Glyph => Application.UsesCamera ? "\uE714" : "\uE720";
}
