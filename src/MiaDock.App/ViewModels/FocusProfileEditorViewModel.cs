using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MiaDock.Core.Focus;
using MiaDock.Core.Localization;
using MiaDock.Core.Modules;

namespace MiaDock.App.ViewModels;

public sealed record FocusIconOption(string Key, string Glyph, string Label);

public sealed class FocusModuleSelectionItemViewModel : ObservableObject
{
    private string _label;
    private bool _isSelected;

    public FocusModuleSelectionItemViewModel(
        string moduleId,
        string label,
        bool isSelected)
    {
        ModuleId = moduleId;
        _label = label;
        _isSelected = isSelected;
    }

    public string ModuleId { get; }

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public sealed class FocusProfileEditorViewModel : ObservableObject, IDisposable
{
    private readonly ILocalizationService _localization;
    private readonly IReadOnlyList<(string Id, string DisplayNameKey)> _moduleDefinitions;
    private readonly FocusProfile _source;
    private readonly IApplicationActivityService? _applications;
    private string _name;
    private string _colorHex;
    private bool _hasDefaultDuration;
    private double _defaultDurationMinutes;
    private bool _allowAllModules;
    private bool _allowFullscreenNotifications;
    private bool _allowSensitiveContentInFullscreen;
    private bool _allowSensitiveContentWhenLocked;
    private int _iconIndex;
    private int _dockVisibilityIndex;
    private int _minimumPriorityIndex;
    private string _errorMessage = string.Empty;
    private bool _disposed;

    public FocusProfileEditorViewModel(
        FocusProfile source,
        bool isNew,
        IReadOnlyList<(string Id, string DisplayNameKey)> moduleDefinitions,
        ILocalizationService localization,
        IApplicationActivityService? applications = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        ArgumentNullException.ThrowIfNull(moduleDefinitions);
        _moduleDefinitions = moduleDefinitions
            .Concat(source.Behavior.AllowedModuleIds
                .Where(id => moduleDefinitions.All(definition =>
                    !definition.Id.Equals(id, StringComparison.Ordinal)))
                .Select(id => (
                    Id: id,
                    DisplayNameKey: $"Module.{id}.Name")))
            .DistinctBy(definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
        _localization = localization ??
            throw new ArgumentNullException(nameof(localization));
        _applications = applications;
        IsNew = isNew;
        IsBuiltIn = FocusProfileDefaults.BuiltInIds.Contains(source.Id);
        _name = IsBuiltIn
            ? DisplayName(source)
            : source.CustomName ?? string.Empty;
        _colorHex = source.Color;
        _hasDefaultDuration = source.DefaultDurationMinutes is not null;
        _defaultDurationMinutes = source.DefaultDurationMinutes ?? 30;
        _allowAllModules = source.Behavior.AllowedModuleIds.Count == 0;
        _allowFullscreenNotifications = source.Behavior.AllowFullscreenNotifications;
        _allowSensitiveContentInFullscreen =
            source.Behavior.AllowSensitiveContentInFullscreen;
        _allowSensitiveContentWhenLocked =
            source.Behavior.AllowSensitiveContentWhenLocked;

        RebuildLocalizedOptions(source.IconKey, source.Behavior.DockVisibility,
            source.Behavior.MinimumEventPriority);
        foreach (var definition in _moduleDefinitions)
        {
            Modules.Add(new FocusModuleSelectionItemViewModel(
                definition.Id,
                Text(definition.DisplayNameKey),
                _allowAllModules ||
                source.Behavior.AllowedModuleIds.Contains(
                    definition.Id,
                    StringComparer.Ordinal)));
        }

        foreach (var schedule in source.Schedules)
        {
            Schedules.Add(new FocusScheduleEditorViewModel(schedule));
        }

        var applicationState =
            _applications?.Current ?? ApplicationActivitySnapshot.Empty;
        foreach (var rule in source.ActivationRules)
        {
            AutomationRules.Add(new FocusAutomationRuleEditorViewModel(
                rule,
                applicationState.AvailableApplications,
                applicationState.IsProcessMonitoringAvailable,
                _localization));
        }

        _localization.LanguageChanged += OnLanguageChanged;
        if (_applications is not null)
        {
            _applications.ActivityChanged += OnApplicationActivityChanged;
        }
    }

    public bool IsNew { get; }

    public bool IsBuiltIn { get; }

    public bool CanEditName => !IsBuiltIn;

    public string ProfileId => _source.Id;

    public ObservableCollection<FocusIconOption> IconOptions { get; } = [];

    public ObservableCollection<FocusModuleSelectionItemViewModel> Modules { get; } = [];

    public ObservableCollection<FocusScheduleEditorViewModel> Schedules { get; } = [];

    public ObservableCollection<FocusAutomationRuleEditorViewModel> AutomationRules
    {
        get;
    } = [];

    public bool CanAddSchedule => Schedules.Count < 16;

    public bool CanAddAutomationRule => AutomationRules.Count < 16;

    public bool IsApplicationMonitoringAvailable =>
        _applications?.Current.IsProcessMonitoringAvailable == true;

    public IReadOnlyList<SettingOption<FocusDockVisibility>> DockVisibilityOptions
    {
        get;
        private set;
    } = [];

    public IReadOnlyList<SettingOption<ModuleEventPriority>> PriorityOptions
    {
        get;
        private set;
    } = [];

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value ?? string.Empty);
    }

    public string ColorHex
    {
        get => _colorHex;
        set => SetProperty(ref _colorHex, value ?? string.Empty);
    }

    public bool HasDefaultDuration
    {
        get => _hasDefaultDuration;
        set => SetProperty(ref _hasDefaultDuration, value);
    }

    public double DefaultDurationMinutes
    {
        get => _defaultDurationMinutes;
        set => SetProperty(ref _defaultDurationMinutes, value);
    }

    public bool AllowAllModules
    {
        get => _allowAllModules;
        set
        {
            if (SetProperty(ref _allowAllModules, value))
            {
                OnPropertyChanged(nameof(CanSelectModules));
            }
        }
    }

    public bool CanSelectModules => !AllowAllModules;

    public bool AllowFullscreenNotifications
    {
        get => _allowFullscreenNotifications;
        set => SetProperty(ref _allowFullscreenNotifications, value);
    }

    public bool AllowSensitiveContentInFullscreen
    {
        get => _allowSensitiveContentInFullscreen;
        set => SetProperty(ref _allowSensitiveContentInFullscreen, value);
    }

    public bool AllowSensitiveContentWhenLocked
    {
        get => _allowSensitiveContentWhenLocked;
        set => SetProperty(ref _allowSensitiveContentWhenLocked, value);
    }

    public int IconIndex
    {
        get => _iconIndex;
        set => SetProperty(
            ref _iconIndex,
            Math.Clamp(value, 0, Math.Max(0, IconOptions.Count - 1)));
    }

    public int DockVisibilityIndex
    {
        get => _dockVisibilityIndex;
        set => SetProperty(
            ref _dockVisibilityIndex,
            Math.Clamp(value, 0, Math.Max(0, DockVisibilityOptions.Count - 1)));
    }

    public int MinimumPriorityIndex
    {
        get => _minimumPriorityIndex;
        set => SetProperty(
            ref _minimumPriorityIndex,
            Math.Clamp(value, 0, Math.Max(0, PriorityOptions.Count - 1)));
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool TryBuildProfile(out FocusProfile profile, out string errorKey)
    {
        var name = Name.Trim();
        if (!IsBuiltIn && (name.Length == 0 || name.Length > 40))
        {
            return Fail(
                "Focus.Settings.Error.Name",
                out profile,
                out errorKey);
        }

        var color = ColorHex.Trim().ToUpperInvariant();
        if (color.Length != 7 ||
            color[0] != '#' ||
            !color[1..].All(Uri.IsHexDigit))
        {
            return Fail(
                "Focus.Settings.Error.Color",
                out profile,
                out errorKey);
        }

        int? duration = null;
        if (HasDefaultDuration)
        {
            if (!double.IsFinite(DefaultDurationMinutes) ||
                DefaultDurationMinutes < 1 ||
                DefaultDurationMinutes > 1440)
            {
                return Fail(
                    "Focus.Settings.Error.Duration",
                    out profile,
                    out errorKey);
            }

            duration = checked((int)Math.Round(DefaultDurationMinutes));
        }

        var selectedModules = AllowAllModules
            ? Array.Empty<string>()
            : Modules
                .Where(item => item.IsSelected)
                .Select(item => item.ModuleId)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        if (!AllowAllModules && selectedModules.Length == 0)
        {
            return Fail(
                "Focus.Settings.Error.Modules",
                out profile,
                out errorKey);
        }

        var schedules = new List<FocusSchedule>(Schedules.Count);
        foreach (var scheduleEditor in Schedules)
        {
            if (!scheduleEditor.TryBuild(out var schedule))
            {
                return Fail(
                    "Focus.Settings.Error.ScheduleDays",
                    out profile,
                    out errorKey);
            }

            schedules.Add(schedule);
        }

        var automationRules =
            new List<FocusActivationRule>(AutomationRules.Count);
        foreach (var ruleEditor in AutomationRules)
        {
            if (!ruleEditor.TryBuild(out var rule))
            {
                return Fail(
                    "Focus.Settings.Error.AutomationTarget",
                    out profile,
                    out errorKey);
            }

            automationRules.Add(rule);
        }

        var icon = IconOptions.ElementAtOrDefault(IconIndex) ??
                   IconOptions.First();
        var visibility = DockVisibilityOptions.ElementAtOrDefault(
            DockVisibilityIndex)?.Value ?? FocusDockVisibility.UseGlobalSetting;
        var priority = PriorityOptions.ElementAtOrDefault(
            MinimumPriorityIndex)?.Value ?? ModuleEventPriority.Low;
        profile = _source with
        {
            CustomName = IsBuiltIn ? null : name,
            IconKey = icon.Key,
            Color = color,
            DefaultDurationMinutes = duration,
            Behavior = new FocusProfileBehavior(
                visibility,
                selectedModules,
                priority,
                AllowFullscreenNotifications,
                AllowSensitiveContentInFullscreen,
                AllowSensitiveContentWhenLocked),
            Schedules = schedules,
            ActivationRules = automationRules
        };
        ErrorMessage = string.Empty;
        errorKey = string.Empty;
        return true;
    }

    public void SetError(string errorKey)
    {
        ErrorMessage = Text(errorKey);
    }

    public void AddSchedule()
    {
        if (!CanAddSchedule)
        {
            return;
        }

        Schedules.Add(new FocusScheduleEditorViewModel(
            new FocusSchedule(
                $"schedule-{Guid.NewGuid():N}",
                true,
                FocusDays.Weekdays,
                9 * 60,
                17 * 60)));
        OnPropertyChanged(nameof(CanAddSchedule));
    }

    public void RemoveSchedule(FocusScheduleEditorViewModel schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        if (Schedules.Remove(schedule))
        {
            OnPropertyChanged(nameof(CanAddSchedule));
        }
    }

    public void AddAutomationRule()
    {
        if (!CanAddAutomationRule)
        {
            return;
        }

        var state = _applications?.Current ?? ApplicationActivitySnapshot.Empty;
        AutomationRules.Add(new FocusAutomationRuleEditorViewModel(
            new FocusActivationRule(
                $"rule-{Guid.NewGuid():N}",
                true,
                FocusActivationRuleKind.ApplicationForeground,
                state.AvailableApplications.FirstOrDefault()?.Target),
            state.AvailableApplications,
            state.IsProcessMonitoringAvailable,
            _localization));
        OnPropertyChanged(nameof(CanAddAutomationRule));
    }

    public void RemoveAutomationRule(FocusAutomationRuleEditorViewModel rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (AutomationRules.Remove(rule))
        {
            rule.Dispose();
            OnPropertyChanged(nameof(CanAddAutomationRule));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _localization.LanguageChanged -= OnLanguageChanged;
        if (_applications is not null)
        {
            _applications.ActivityChanged -= OnApplicationActivityChanged;
        }

        foreach (var rule in AutomationRules)
        {
            rule.Dispose();
        }
    }

    private bool Fail(
        string errorKey,
        out FocusProfile profile,
        out string returnedErrorKey)
    {
        profile = _source;
        returnedErrorKey = errorKey;
        SetError(errorKey);
        return false;
    }

    private void OnLanguageChanged(object? sender, EventArgs args)
    {
        var iconKey = IconOptions.ElementAtOrDefault(IconIndex)?.Key ??
                      _source.IconKey;
        var visibility = DockVisibilityOptions.ElementAtOrDefault(
            DockVisibilityIndex)?.Value ?? _source.Behavior.DockVisibility;
        var priority = PriorityOptions.ElementAtOrDefault(
            MinimumPriorityIndex)?.Value ?? _source.Behavior.MinimumEventPriority;
        if (IsBuiltIn)
        {
            Name = DisplayName(_source);
        }

        RebuildLocalizedOptions(iconKey, visibility, priority);
        foreach (var module in Modules)
        {
            var definition = _moduleDefinitions.First(item =>
                item.Id.Equals(module.ModuleId, StringComparison.Ordinal));
            module.Label = Text(definition.DisplayNameKey);
        }

        if (HasError)
        {
            ErrorMessage = string.Empty;
        }
    }

    private void OnApplicationActivityChanged(
        object? sender,
        ApplicationActivitySnapshot snapshot)
    {
        foreach (var rule in AutomationRules)
        {
            rule.UpdateApplications(
                snapshot.AvailableApplications,
                snapshot.IsProcessMonitoringAvailable);
        }

        OnPropertyChanged(nameof(IsApplicationMonitoringAvailable));
    }

    private void RebuildLocalizedOptions(
        string iconKey,
        FocusDockVisibility visibility,
        ModuleEventPriority priority)
    {
        IconOptions.Clear();
        foreach (var key in new[]
                 {
                     "briefcase", "game-controller", "moon", "do-not-disturb",
                     "star", "book", "fitness", "leaf"
                 })
        {
            IconOptions.Add(new FocusIconOption(
                key,
                FocusIconGlyphs.For(key),
                Text($"Focus.Icon.{key}")));
        }

        DockVisibilityOptions =
        [
            new(FocusDockVisibility.UseGlobalSetting,
                Text("Focus.Visibility.Global")),
            new(FocusDockVisibility.AlwaysVisible,
                Text("Focus.Visibility.Always")),
            new(FocusDockVisibility.EventsOnly,
                Text("Focus.Visibility.EventsOnly")),
            new(FocusDockVisibility.Hidden,
                Text("Focus.Visibility.Hidden"))
        ];
        PriorityOptions =
        [
            new(ModuleEventPriority.Low, Text("Focus.Priority.Low")),
            new(ModuleEventPriority.Normal, Text("Focus.Priority.Normal")),
            new(ModuleEventPriority.Elevated, Text("Focus.Priority.Elevated")),
            new(ModuleEventPriority.High, Text("Focus.Priority.High")),
            new(ModuleEventPriority.Critical, Text("Focus.Priority.Critical"))
        ];
        IconIndex = Math.Max(0, IconOptions
            .Select((item, index) => (item, index))
            .FirstOrDefault(pair => pair.item.Key == iconKey).index);
        DockVisibilityIndex = Math.Max(0, IndexOf(
            DockVisibilityOptions,
            visibility));
        MinimumPriorityIndex = Math.Max(0, IndexOf(
            PriorityOptions,
            priority));
        OnPropertyChanged(nameof(DockVisibilityOptions));
        OnPropertyChanged(nameof(PriorityOptions));
    }

    private string DisplayName(FocusProfile profile) =>
        profile.Kind == FocusProfileKind.Custom
            ? profile.CustomName ?? Text("Focus.Profile.Custom.Name")
            : Text(FocusProfileDefaults.GetDisplayNameKey(profile));

    private string Text(string key) => _localization.Get(key);

    private static int IndexOf<T>(
        IReadOnlyList<SettingOption<T>> options,
        T value) where T : struct, Enum
    {
        for (var index = 0; index < options.Count; index++)
        {
            if (EqualityComparer<T>.Default.Equals(options[index].Value, value))
            {
                return index;
            }
        }

        return -1;
    }
}
