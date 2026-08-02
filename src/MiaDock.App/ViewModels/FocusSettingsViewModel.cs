using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MiaDock.App.Services;
using MiaDock.Core.Focus;
using MiaDock.Core.Localization;
using MiaDock.Core.Modules;

namespace MiaDock.App.ViewModels;

public enum FocusProfileSaveResult
{
    Success,
    Invalid,
    DuplicateName,
    LimitReached,
    ProtectedProfile,
    NotFound
}

public sealed class FocusSettingsViewModel : ObservableObject, IDisposable
{
    public const int MaximumProfiles = 16;
    public const int MaximumCustomProfiles =
        MaximumProfiles - 4;

    private readonly ISettingsService _settings;
    private readonly ILocalizationService _localization;
    private readonly IReadOnlyList<(string Id, string DisplayNameKey)> _moduleDefinitions;
    private readonly IApplicationActivityService? _applications;
    private readonly IFocusSettingsLauncher? _focusSettingsLauncher;
    private bool _disposed;

    public FocusSettingsViewModel(
        ISettingsService settings,
        ILocalizationService localization,
        IEnumerable<IIslandModule>? modules = null,
        IApplicationActivityService? applications = null,
        IFocusSettingsLauncher? focusSettingsLauncher = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _localization = localization ??
            throw new ArgumentNullException(nameof(localization));
        _applications = applications;
        _focusSettingsLauncher = focusSettingsLauncher;
        OpenWindowsFocusSettingsCommand = new AsyncRelayCommand(
            OpenWindowsFocusSettingsAsync,
            () => _focusSettingsLauncher is not null);
        _moduleDefinitions = (modules ?? Array.Empty<IIslandModule>())
            .Select(module => (
                module.Descriptor.Id,
                module.Descriptor.DisplayNameKey))
            .DistinctBy(item => item.Id, StringComparer.Ordinal)
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .ToArray();
        _settings.SettingsChanged += OnSettingsChanged;
        _localization.LanguageChanged += OnLanguageChanged;
        Refresh(_settings.Current.Focus);
    }

    public ObservableCollection<FocusSettingsProfileItemViewModel> Profiles
    {
        get;
    } = [];

    public IAsyncRelayCommand OpenWindowsFocusSettingsCommand { get; }

    public bool CanCreateProfile =>
        Profiles.Count < MaximumProfiles &&
        Profiles.Count(item => item.IsCustom) < MaximumCustomProfiles;

    public string ProfileCountSummary => Text(
        "Focus.Settings.ProfileCount",
        Profiles.Count,
        MaximumProfiles);

    public bool HasAutomationConflicts =>
        CountAutomationConflicts(_settings.Current.Focus.Profiles) > 0;

    public string AutomationConflictMessage => Text(
        "Focus.Settings.AutomationConflict",
        CountAutomationConflicts(_settings.Current.Focus.Profiles));

    public FocusProfileEditorViewModel? CreateNewEditor()
    {
        if (!CanCreateProfile)
        {
            return null;
        }

        var profile = new FocusProfile(
            $"custom-{Guid.NewGuid():N}",
            FocusProfileKind.Custom,
            string.Empty,
            "star",
            "#0EA5E9",
            null,
            new FocusProfileBehavior(
                FocusDockVisibility.UseGlobalSetting,
                Array.Empty<string>(),
                ModuleEventPriority.Low,
                true,
                false,
                false),
            Array.Empty<FocusSchedule>(),
            Array.Empty<FocusActivationRule>());
        return new FocusProfileEditorViewModel(
            profile,
            isNew: true,
            _moduleDefinitions,
            _localization,
            _applications);
    }

    public FocusProfileEditorViewModel? CreateEditor(string profileId)
    {
        var profile = _settings.Current.Focus.Profiles.FirstOrDefault(item =>
            item.Id.Equals(profileId, StringComparison.Ordinal));
        return profile is null
            ? null
            : new FocusProfileEditorViewModel(
                profile,
                isNew: false,
                _moduleDefinitions,
                _localization,
                _applications);
    }

    public FocusProfileSaveResult Save(FocusProfileEditorViewModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        if (!editor.TryBuildProfile(out var profile, out _))
        {
            return FocusProfileSaveResult.Invalid;
        }

        var currentProfiles = _settings.Current.Focus.Profiles;
        var existingIndex = currentProfiles
            .Select((item, index) => (item, index))
            .FirstOrDefault(pair =>
                pair.item.Id.Equals(profile.Id, StringComparison.Ordinal))
            .index;
        var exists = currentProfiles.Any(item =>
            item.Id.Equals(profile.Id, StringComparison.Ordinal));
        if (editor.IsNew && exists)
        {
            return FocusProfileSaveResult.Invalid;
        }

        if (!editor.IsNew && !exists)
        {
            return FocusProfileSaveResult.NotFound;
        }

        if (profile.Kind == FocusProfileKind.Custom &&
            currentProfiles.Any(item =>
                item.Kind == FocusProfileKind.Custom &&
                !item.Id.Equals(profile.Id, StringComparison.Ordinal) &&
                string.Equals(
                    item.CustomName?.Trim(),
                    profile.CustomName?.Trim(),
                    StringComparison.CurrentCultureIgnoreCase)))
        {
            editor.SetError("Focus.Settings.Error.DuplicateName");
            return FocusProfileSaveResult.DuplicateName;
        }

        if (editor.IsNew && !CanCreateProfile)
        {
            editor.SetError("Focus.Settings.Error.Limit");
            return FocusProfileSaveResult.LimitReached;
        }

        _settings.Update(settings =>
        {
            var profiles = settings.Focus.Profiles.ToList();
            if (editor.IsNew)
            {
                profiles.Add(profile);
            }
            else
            {
                profiles[existingIndex] = profile;
            }

            return settings with
            {
                Focus = settings.Focus with { Profiles = profiles }
            };
        });
        return FocusProfileSaveResult.Success;
    }

    public FocusProfileSaveResult Delete(string profileId)
    {
        var profile = _settings.Current.Focus.Profiles.FirstOrDefault(item =>
            item.Id.Equals(profileId, StringComparison.Ordinal));
        if (profile is null)
        {
            return FocusProfileSaveResult.NotFound;
        }

        if (FocusProfileDefaults.BuiltInIds.Contains(profile.Id))
        {
            return FocusProfileSaveResult.ProtectedProfile;
        }

        _settings.Update(settings => settings with
        {
            Focus = settings.Focus with
            {
                Profiles = settings.Focus.Profiles
                    .Where(item => !item.Id.Equals(
                        profileId,
                        StringComparison.Ordinal))
                    .ToArray()
            }
        });
        return FocusProfileSaveResult.Success;
    }

    public FocusProfileSaveResult ResetBuiltIn(string profileId)
    {
        var original = FocusProfileDefaults.FindBuiltIn(profileId);
        if (original is null)
        {
            return FocusProfileSaveResult.ProtectedProfile;
        }

        var index = _settings.Current.Focus.Profiles
            .Select((item, itemIndex) => (item, itemIndex))
            .FirstOrDefault(pair =>
                pair.item.Id.Equals(profileId, StringComparison.Ordinal))
            .itemIndex;
        if (!_settings.Current.Focus.Profiles.Any(item =>
                item.Id.Equals(profileId, StringComparison.Ordinal)))
        {
            return FocusProfileSaveResult.NotFound;
        }

        _settings.Update(settings =>
        {
            var profiles = settings.Focus.Profiles.ToList();
            profiles[index] = original;
            return settings with
            {
                Focus = settings.Focus with { Profiles = profiles }
            };
        });
        return FocusProfileSaveResult.Success;
    }

    public bool IsActive(string profileId) =>
        string.Equals(
            _settings.Current.Focus.ActiveState?.ProfileId,
            profileId,
            StringComparison.Ordinal);

    private async Task OpenWindowsFocusSettingsAsync()
    {
        if (_focusSettingsLauncher is not null)
        {
            await _focusSettingsLauncher.OpenWindowsFocusSettingsAsync();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settings.SettingsChanged -= OnSettingsChanged;
        _localization.LanguageChanged -= OnLanguageChanged;
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        if (args.Previous.Focus != args.Current.Focus)
        {
            Refresh(args.Current.Focus);
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs args) =>
        Refresh(_settings.Current.Focus);

    private void Refresh(MiaDock.Core.Settings.FocusSettings settings)
    {
        var existing = Profiles.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var activeId = settings.ActiveState?.ProfileId;
        var ordered = new List<FocusSettingsProfileItemViewModel>(
            settings.Profiles.Count);
        foreach (var profile in settings.Profiles)
        {
            var displayName = DisplayName(profile);
            var summary = Summary(profile);
            if (!existing.TryGetValue(profile.Id, out var item))
            {
                item = new FocusSettingsProfileItemViewModel(
                    profile,
                    displayName,
                    summary,
                    profile.Id == activeId);
            }
            else
            {
                item.Refresh(
                    profile,
                    displayName,
                    summary,
                    profile.Id == activeId);
            }

            ordered.Add(item);
        }

        Profiles.Clear();
        foreach (var item in ordered)
        {
            Profiles.Add(item);
        }

        OnPropertyChanged(nameof(CanCreateProfile));
        OnPropertyChanged(nameof(ProfileCountSummary));
        OnPropertyChanged(nameof(HasAutomationConflicts));
        OnPropertyChanged(nameof(AutomationConflictMessage));
    }

    private string DisplayName(FocusProfile profile) =>
        profile.Kind == FocusProfileKind.Custom
            ? profile.CustomName ?? Text("Focus.Profile.Custom.Name")
            : Text(FocusProfileDefaults.GetDisplayNameKey(profile));

    private string Summary(FocusProfile profile)
    {
        var visibility = profile.Behavior.DockVisibility switch
        {
            FocusDockVisibility.AlwaysVisible =>
                Text("Focus.Visibility.Always"),
            FocusDockVisibility.EventsOnly =>
                Text("Focus.Visibility.EventsOnly"),
            FocusDockVisibility.Hidden =>
                Text("Focus.Visibility.Hidden"),
            _ => Text("Focus.Visibility.Global")
        };
        var duration = profile.DefaultDurationMinutes is { } minutes
            ? Text("Focus.Settings.Minutes", minutes)
            : Text("Focus.Duration.UntilTurnedOff");
        var triggerCount =
            profile.Schedules.Count(schedule => schedule.IsEnabled) +
            profile.ActivationRules.Count(rule => rule.IsEnabled);
        return triggerCount switch
        {
            0 => Text("Focus.Settings.ProfileSummary", visibility, duration),
            1 => Text(
                "Focus.Settings.ProfileSummary.OneAutomation",
                visibility,
                duration),
            _ => Text(
                "Focus.Settings.ProfileSummary.Automated",
                visibility,
                duration,
                triggerCount)
        };
    }

    private static int CountAutomationConflicts(
        IReadOnlyList<FocusProfile> profiles) =>
        profiles
            .SelectMany(profile => profile.ActivationRules
                .Where(rule => rule.IsEnabled)
                .Select(rule => new
                {
                    ProfileId = profile.Id,
                    rule.Kind,
                    Target = rule.Target ?? "*"
                }))
            .GroupBy(item => (item.Kind, item.Target))
            .Count(group =>
                group.Select(item => item.ProfileId)
                    .Distinct(StringComparer.Ordinal)
                    .Skip(1)
                    .Any());

    private string Text(string key, params object?[] arguments) =>
        _localization.Get(key, arguments);
}
