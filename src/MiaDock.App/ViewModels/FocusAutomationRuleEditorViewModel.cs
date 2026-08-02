using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.Core.Focus;
using MiaDock.Core.Localization;

namespace MiaDock.App.ViewModels;

public sealed record FocusApplicationOption(string? Target, string Label);

public sealed class FocusAutomationRuleEditorViewModel : ObservableObject, IDisposable
{
    private readonly ILocalizationService _localization;
    private readonly string _sourceTarget;
    private bool _isEnabled;
    private int _kindIndex;
    private int _applicationIndex;
    private bool _processMonitoringAvailable;
    private bool _disposed;

    public FocusAutomationRuleEditorViewModel(
        FocusActivationRule rule,
        IReadOnlyList<FocusApplicationInfo> applications,
        bool processMonitoringAvailable,
        ILocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _localization = localization ??
            throw new ArgumentNullException(nameof(localization));
        Id = rule.Id;
        _sourceTarget = rule.Target ?? string.Empty;
        _isEnabled = rule.IsEnabled;
        _processMonitoringAvailable = processMonitoringAvailable;
        RebuildOptions(rule.Kind, rule.Target, applications);
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public string Id { get; }

    public IReadOnlyList<SettingOption<FocusActivationRuleKind>> KindOptions
    {
        get;
        private set;
    } = [];

    public IReadOnlyList<FocusApplicationOption> ApplicationOptions
    {
        get;
        private set;
    } = [];

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public int KindIndex
    {
        get => _kindIndex;
        set
        {
            var next = Math.Clamp(value, 0, Math.Max(0, KindOptions.Count - 1));
            if (SetProperty(ref _kindIndex, next))
            {
                if (!AllowsAnyApplication &&
                    ApplicationOptions.ElementAtOrDefault(ApplicationIndex)?.Target is null)
                {
                    ApplicationIndex = Math.Max(
                        0,
                        ApplicationOptions
                            .Select((item, index) => (item, index))
                            .FirstOrDefault(pair => pair.item.Target is not null)
                            .index);
                }

                OnPropertyChanged(nameof(AllowsAnyApplication));
                OnPropertyChanged(nameof(AvailabilityMessage));
                OnPropertyChanged(nameof(HasAvailabilityMessage));
            }
        }
    }

    public int ApplicationIndex
    {
        get => _applicationIndex;
        set => SetProperty(
            ref _applicationIndex,
            Math.Clamp(value, 0, Math.Max(0, ApplicationOptions.Count - 1)));
    }

    public bool AllowsAnyApplication =>
        SelectedKind == FocusActivationRuleKind.FullscreenApplication;

    public string AvailabilityMessage =>
        SelectedKind == FocusActivationRuleKind.ApplicationRunning &&
        !_processMonitoringAvailable
            ? Text("Focus.Automation.ProcessUnavailable")
            : string.Empty;

    public bool HasAvailabilityMessage =>
        !string.IsNullOrEmpty(AvailabilityMessage);

    public FocusActivationRuleKind SelectedKind =>
        KindOptions.ElementAtOrDefault(KindIndex)?.Value ??
        FocusActivationRuleKind.ApplicationForeground;

    public bool TryBuild(out FocusActivationRule rule)
    {
        var target = ApplicationOptions.ElementAtOrDefault(ApplicationIndex)?.Target;
        if (SelectedKind != FocusActivationRuleKind.FullscreenApplication &&
            string.IsNullOrWhiteSpace(target))
        {
            rule = new FocusActivationRule(
                Id,
                IsEnabled,
                SelectedKind,
                null);
            return false;
        }

        rule = new FocusActivationRule(
            Id,
            IsEnabled,
            SelectedKind,
            string.IsNullOrWhiteSpace(target) ? null : target);
        return true;
    }

    public void UpdateApplications(
        IReadOnlyList<FocusApplicationInfo> applications,
        bool processMonitoringAvailable)
    {
        var selectedTarget =
            ApplicationOptions.ElementAtOrDefault(ApplicationIndex)?.Target ??
            _sourceTarget;
        _processMonitoringAvailable = processMonitoringAvailable;
        RebuildApplications(selectedTarget, applications);
        OnPropertyChanged(nameof(AvailabilityMessage));
        OnPropertyChanged(nameof(HasAvailabilityMessage));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _localization.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        var kind = SelectedKind;
        var target = ApplicationOptions.ElementAtOrDefault(ApplicationIndex)?.Target;
        RebuildOptions(
            kind,
            target,
            ApplicationOptions
                .Where(option => option.Target is not null)
                .Select(option => new FocusApplicationInfo(
                    option.Target!,
                    option.Label))
                .ToArray());
        OnPropertyChanged(nameof(AvailabilityMessage));
        OnPropertyChanged(nameof(HasAvailabilityMessage));
    }

    private void RebuildOptions(
        FocusActivationRuleKind kind,
        string? target,
        IReadOnlyList<FocusApplicationInfo> applications)
    {
        KindOptions =
        [
            new(
                FocusActivationRuleKind.ApplicationForeground,
                Text("Focus.Automation.Kind.Foreground")),
            new(
                FocusActivationRuleKind.FullscreenApplication,
                Text("Focus.Automation.Kind.Fullscreen")),
            new(
                FocusActivationRuleKind.ApplicationRunning,
                Text("Focus.Automation.Kind.Running"))
        ];
        _kindIndex = Math.Max(
            0,
            KindOptions
                .Select((item, index) => (item, index))
                .FirstOrDefault(pair => pair.item.Value == kind)
                .index);
        RebuildApplications(target, applications);
        OnPropertyChanged(nameof(KindOptions));
        OnPropertyChanged(nameof(AllowsAnyApplication));
    }

    private void RebuildApplications(
        string? target,
        IReadOnlyList<FocusApplicationInfo> applications)
    {
        var normalizedTarget = string.IsNullOrWhiteSpace(target)
            ? null
            : FocusApplicationTarget.Normalize(target);
        var options = new List<FocusApplicationOption>
        {
            new(null, Text("Focus.Automation.AnyApplication"))
        };
        options.AddRange(applications
            .Where(item => !string.IsNullOrWhiteSpace(item.Target))
            .Select(item => new FocusApplicationOption(
                FocusApplicationTarget.Normalize(item.Target),
                item.DisplayName))
            .DistinctBy(item => item.Target, StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase));
        if (normalizedTarget is not null &&
            options.All(option => !string.Equals(
                option.Target,
                normalizedTarget,
                StringComparison.OrdinalIgnoreCase)))
        {
            options.Add(new FocusApplicationOption(
                normalizedTarget,
                Path.GetFileNameWithoutExtension(normalizedTarget)));
        }

        ApplicationOptions = options;
        _applicationIndex = Math.Max(
            0,
            options
                .Select((item, index) => (item, index))
                .FirstOrDefault(pair => string.Equals(
                    pair.item.Target,
                    normalizedTarget,
                    StringComparison.OrdinalIgnoreCase))
                .index);
        if (!AllowsAnyApplication && ApplicationOptions[_applicationIndex].Target is null)
        {
            _applicationIndex = options.Count > 1 ? 1 : 0;
        }

        OnPropertyChanged(nameof(ApplicationOptions));
        OnPropertyChanged(nameof(ApplicationIndex));
    }

    private string Text(string key) => _localization.Get(key);
}
