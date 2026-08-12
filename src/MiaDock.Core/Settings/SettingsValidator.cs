using MiaDock.Core.Focus;
using MiaDock.Core.Modules;
using MiaDock.Core.Presentation;

namespace MiaDock.Core.Settings;

public static class SettingsValidator
{
    public static MiaDockSettings Normalize(MiaDockSettings? settings)
    {
        if (settings is null)
        {
            return MiaDockSettings.Default;
        }

        settings = Migrate(settings);
        var general = settings.General ?? GeneralSettings.Default;
        general = general with
        {
            Language = EnumValue(general.Language, GeneralSettings.Default.Language),
            VisibilityMode = EnumValue(general.VisibilityMode, GeneralSettings.Default.VisibilityMode),
            InteractionMode = EnumValue(general.InteractionMode, GeneralSettings.Default.InteractionMode),
            Position = EnumValue(general.Position, GeneralSettings.Default.Position),
            Clock = NormalizeClock(general.Clock),
            PassiveModuleReturnSeconds = ClampFinite(
                general.PassiveModuleReturnSeconds,
                3,
                30,
                GeneralSettings.Default.PassiveModuleReturnSeconds),
            ShowKeyboardLockEvents = general.ShowKeyboardLockEvents,
            ShowUsbDeviceEvents = general.ShowUsbDeviceEvents
        };

        var appearance = settings.Appearance ?? AppearanceSettings.Default;
        var motion = appearance.Motion ?? MotionSettings.FromLegacy(
            appearance.AnimationKind,
            appearance.AnimationSpeed);
        motion = motion with
        {
            Preset = EnumValue(motion.Preset, MotionSettings.Default.Preset),
            Speed = ClampFinite(motion.Speed, 0.5, 2, MotionSettings.Default.Speed),
            Intensity = ClampFinite(motion.Intensity, 0, 1, MotionSettings.Default.Intensity),
            Springiness = ClampFinite(motion.Springiness, 0, 1, MotionSettings.Default.Springiness),
            ContentDelayMilliseconds = Math.Clamp(motion.ContentDelayMilliseconds, 0, 120)
        };
        var cornerRadii = appearance.EffectiveCornerRadii;
        cornerRadii = new DockCornerRadii(
            ClampFinite(cornerRadii.TopLeft, 0, 48, appearance.CornerRadius),
            ClampFinite(cornerRadii.TopRight, 0, 48, appearance.CornerRadius),
            ClampFinite(cornerRadii.BottomRight, 0, 48, appearance.CornerRadius),
            ClampFinite(cornerRadii.BottomLeft, 0, 48, appearance.CornerRadius));
        if (appearance.LinkCornerRadii)
        {
            cornerRadii = DockCornerRadii.Uniform(cornerRadii.TopLeft);
        }

        appearance = appearance with
        {
            Theme = EnumValue(appearance.Theme, AppearanceSettings.Default.Theme),
            CollapsedWidth = ClampFinite(appearance.CollapsedWidth, 120, 360, AppearanceSettings.Default.CollapsedWidth),
            CollapsedHeight = ClampFinite(appearance.CollapsedHeight, 32, 96, AppearanceSettings.Default.CollapsedHeight),
            HoverWidth = ClampFinite(appearance.HoverWidth, 180, 520, AppearanceSettings.Default.HoverWidth),
            HoverHeight = ClampFinite(appearance.HoverHeight, 48, 160, AppearanceSettings.Default.HoverHeight),
            ExpandedWidth = ClampFinite(appearance.ExpandedWidth, 548, 720, AppearanceSettings.Default.ExpandedWidth),
            ExpandedHeight = ClampFinite(appearance.ExpandedHeight, 360, 420, AppearanceSettings.Default.ExpandedHeight),
            NotificationWidth = ClampFinite(appearance.NotificationWidth, 240, 620, AppearanceSettings.Default.NotificationWidth),
            NotificationHeight = ClampFinite(appearance.NotificationHeight, 64, 180, AppearanceSettings.Default.NotificationHeight),
            CornerRadius = cornerRadii.TopLeft,
            CornerRadii = cornerRadii,
            EdgeMargin = ClampFinite(
                appearance.EdgeMargin,
                0,
                96,
                AppearanceSettings.Default.EdgeMargin),
            BackgroundColor = NormalizeColor(appearance.BackgroundColor, AppearanceSettings.Default.BackgroundColor),
            AccentColor = NormalizeColor(appearance.AccentColor, AppearanceSettings.Default.AccentColor),
            Opacity = ClampFinite(appearance.Opacity, 0.35, 1, AppearanceSettings.Default.Opacity),
            ShadowIntensity = ClampFinite(appearance.ShadowIntensity, 0, 1, AppearanceSettings.Default.ShadowIntensity),
            AnimationSpeed = ClampFinite(appearance.AnimationSpeed, 0.5, 2, AppearanceSettings.Default.AnimationSpeed),
            AnimationKind = EnumValue(appearance.AnimationKind, AppearanceSettings.Default.AnimationKind),
            Motion = motion
        };

        var media = settings.Media ?? MediaSettings.Default;
        media = media with
        {
            Fallback = EnumValue(media.Fallback, MediaSettings.Default.Fallback),
            VolumeTarget = EnumValue(media.VolumeTarget, MediaSettings.Default.VolumeTarget)
        };

        var monitor = settings.Monitor ?? MonitorSettings.Default;
        monitor = monitor with { Mode = EnumValue(monitor.Mode, MonitorSettings.Default.Mode) };

        var startup = settings.StartupShutdown ?? StartupShutdownSettings.Default;
        startup = startup with
        {
            LaunchMode = EnumValue(startup.LaunchMode, StartupShutdownSettings.Default.LaunchMode),
            CloseBehavior = EnumValue(startup.CloseBehavior, StartupShutdownSettings.Default.CloseBehavior)
        };
        var tray = settings.Tray ?? TraySettings.Default;
        if (startup.LaunchMode == StartupLaunchMode.SilentTray && !tray.ShowIcon)
        {
            tray = tray with { ShowIcon = true };
        }

        return settings with
        {
            SchemaVersion = MiaDockSettings.CurrentSchemaVersion,
            General = general,
            Appearance = appearance,
            Media = media,
            Fullscreen = NormalizeFullscreen(settings.Fullscreen),
            Monitor = monitor,
            Tray = tray,
            StartupShutdown = startup,
            Onboarding = NormalizeOnboarding(settings.Onboarding),
            HotKeys = NormalizeHotKeys(settings.HotKeys),
            Privacy = settings.Privacy ?? PresentationPrivacySettings.Default,
            StoreUpdates = NormalizeStoreUpdates(settings.StoreUpdates),
            Focus = NormalizeFocus(settings.Focus),
            Modules = NormalizeModules(settings.Modules)
        };
    }

    private static FocusSettings NormalizeFocus(FocusSettings? value)
    {
        var settings = value ?? FocusSettings.Default;
        var sourceProfiles = settings.Profiles ?? Array.Empty<FocusProfile>();
        var normalizedProfiles = new List<FocusProfile>(capacity: Math.Min(sourceProfiles.Count, 16));
        var profileIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var profile in sourceProfiles)
        {
            if (normalizedProfiles.Count >= 16)
            {
                break;
            }

            if (profile is null)
            {
                continue;
            }

            var normalized = NormalizeFocusProfile(profile);
            if (normalized is null || !profileIds.Add(normalized.Id))
            {
                continue;
            }

            normalized = MigrateFocusProfile(settings.SchemaVersion, normalized);
            normalizedProfiles.Add(normalized);
        }

        foreach (var builtIn in FocusProfileDefaults.All)
        {
            if (profileIds.Contains(builtIn.Id))
            {
                continue;
            }

            if (normalizedProfiles.Count >= 16)
            {
                var removableIndex = normalizedProfiles.FindLastIndex(
                    profile => !FocusProfileDefaults.BuiltInIds.Contains(profile.Id));
                if (removableIndex >= 0)
                {
                    profileIds.Remove(normalizedProfiles[removableIndex].Id);
                    normalizedProfiles.RemoveAt(removableIndex);
                }
            }

            if (normalizedProfiles.Count < 16)
            {
                normalizedProfiles.Add(builtIn);
                profileIds.Add(builtIn.Id);
            }
        }

        var activeState = settings.IsEnabled
            ? NormalizeFocusActivationState(settings.ActiveState, profileIds)
            : null;
        var profilesUnchanged =
            settings.Profiles is not null &&
            normalizedProfiles.Count == settings.Profiles.Count &&
            normalizedProfiles.SequenceEqual(settings.Profiles);

        if (settings.SchemaVersion == FocusSettings.CurrentSchemaVersion &&
            profilesUnchanged &&
            ReferenceEquals(activeState, settings.ActiveState))
        {
            return settings;
        }

        return new FocusSettings(
            FocusSettings.CurrentSchemaVersion,
            normalizedProfiles,
            activeState,
            settings.IsEnabled);
    }

    private static FocusProfile MigrateFocusProfile(
        int sourceSchemaVersion,
        FocusProfile profile)
    {
        if (sourceSchemaVersion >= 3 ||
            !string.Equals(
                profile.Id,
                FocusProfileDefaults.DoNotDisturbId,
                StringComparison.Ordinal))
        {
            return profile;
        }

        var behavior = profile.Behavior;
        var usesLegacyDefault =
            behavior.DockVisibility == FocusDockVisibility.EventsOnly &&
            behavior.AllowedModuleIds.SequenceEqual(["battery", "timer"]) &&
            behavior.MinimumEventPriority == ModuleEventPriority.High &&
            !behavior.AllowFullscreenNotifications &&
            !behavior.AllowSensitiveContentInFullscreen &&
            !behavior.AllowSensitiveContentWhenLocked;
        return usesLegacyDefault
            ? profile with
            {
                Behavior = behavior with
                {
                    DockVisibility = FocusDockVisibility.UseGlobalSetting
                }
            }
            : profile;
    }

    private static FocusProfile? NormalizeFocusProfile(FocusProfile profile)
    {
        var id = NormalizeIdentifier(profile.Id);
        if (id is null)
        {
            return null;
        }

        var builtIn = FocusProfileDefaults.FindBuiltIn(id);
        var kind = builtIn?.Kind ?? EnumValue(profile.Kind, FocusProfileKind.Custom);
        var customName = kind == FocusProfileKind.Custom
            ? NormalizeCustomFocusName(profile.CustomName)
            : null;
        if (kind == FocusProfileKind.Custom && customName is null)
        {
            return null;
        }

        var fallbackIcon = builtIn?.IconKey ?? "star";
        var iconKey = profile.IconKey?.Trim().ToLowerInvariant();
        if (iconKey is null || !FocusProfileDefaults.AllowedIconKeys.Contains(iconKey))
        {
            iconKey = fallbackIcon;
        }

        var color = NormalizeColor(profile.Color, builtIn?.Color ?? "#0EA5E9");
        int? duration = profile.DefaultDurationMinutes is { } durationMinutes
            ? Math.Clamp(durationMinutes, 1, 24 * 60)
            : null;
        var behavior = NormalizeFocusBehavior(profile.Behavior, builtIn?.Behavior);
        var schedules = NormalizeFocusSchedules(profile.Schedules);
        var activationRules = NormalizeFocusActivationRules(profile.ActivationRules);

        if (id == profile.Id &&
            kind == profile.Kind &&
            customName == profile.CustomName &&
            iconKey == profile.IconKey &&
            color == profile.Color &&
            duration == profile.DefaultDurationMinutes &&
            behavior == profile.Behavior &&
            schedules == profile.Schedules &&
            activationRules == profile.ActivationRules)
        {
            return profile;
        }

        return new FocusProfile(
            id,
            kind,
            customName,
            iconKey,
            color,
            duration,
            behavior,
            schedules,
            activationRules);
    }

    private static FocusProfileBehavior NormalizeFocusBehavior(
        FocusProfileBehavior? value,
        FocusProfileBehavior? fallback)
    {
        var behavior = value ?? fallback ?? FocusProfileBehavior.Default;
        var sourceModules = behavior.AllowedModuleIds ?? Array.Empty<string>();
        var moduleIds = sourceModules
            .Select(NormalizeIdentifier)
            .Where(id => id is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(64)
            .ToArray();
        var modulesUnchanged =
            behavior.AllowedModuleIds is not null &&
            moduleIds.SequenceEqual(behavior.AllowedModuleIds);
        var visibility = EnumValue(behavior.DockVisibility, FocusDockVisibility.UseGlobalSetting);
        var priority = EnumValue(behavior.MinimumEventPriority, ModuleEventPriority.Low);

        if (modulesUnchanged &&
            visibility == behavior.DockVisibility &&
            priority == behavior.MinimumEventPriority)
        {
            return behavior;
        }

        return behavior with
        {
            DockVisibility = visibility,
            AllowedModuleIds = moduleIds,
            MinimumEventPriority = priority
        };
    }

    private static IReadOnlyList<FocusSchedule> NormalizeFocusSchedules(
        IReadOnlyList<FocusSchedule>? schedules)
    {
        if (schedules is null)
        {
            return Array.Empty<FocusSchedule>();
        }

        var normalized = new List<FocusSchedule>(Math.Min(schedules.Count, 16));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var schedule in schedules)
        {
            if (normalized.Count >= 16)
            {
                break;
            }

            if (schedule is null)
            {
                continue;
            }

            var id = NormalizeIdentifier(schedule.Id);
            var days = schedule.Days & FocusDays.EveryDay;
            if (id is null || days == FocusDays.None || !ids.Add(id))
            {
                continue;
            }

            var startMinute = Math.Clamp(schedule.StartMinute, 0, 1439);
            var endMinute = Math.Clamp(schedule.EndMinute, 0, 1439);
            normalized.Add(
                id == schedule.Id &&
                days == schedule.Days &&
                startMinute == schedule.StartMinute &&
                endMinute == schedule.EndMinute
                    ? schedule
                    : schedule with
                    {
                        Id = id,
                        Days = days,
                        StartMinute = startMinute,
                        EndMinute = endMinute
                    });
        }

        return normalized.Count == schedules.Count && normalized.SequenceEqual(schedules)
            ? schedules
            : normalized;
    }

    private static IReadOnlyList<FocusActivationRule> NormalizeFocusActivationRules(
        IReadOnlyList<FocusActivationRule>? rules)
    {
        if (rules is null)
        {
            return Array.Empty<FocusActivationRule>();
        }

        var normalized = new List<FocusActivationRule>(Math.Min(rules.Count, 16));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in rules)
        {
            if (normalized.Count >= 16)
            {
                break;
            }

            if (rule is null)
            {
                continue;
            }

            var id = NormalizeIdentifier(rule.Id);
            if (id is null || !ids.Add(id))
            {
                continue;
            }

            var kind = EnumValue(rule.Kind, FocusActivationRuleKind.ApplicationForeground);
            var target = NormalizeFocusRuleTarget(rule.Target);
            if (kind is FocusActivationRuleKind.ApplicationRunning or
                FocusActivationRuleKind.ApplicationForeground &&
                target is null)
            {
                continue;
            }

            normalized.Add(
                id == rule.Id && kind == rule.Kind && target == rule.Target
                    ? rule
                    : rule with { Id = id, Kind = kind, Target = target });
        }

        return normalized.Count == rules.Count && normalized.SequenceEqual(rules)
            ? rules
            : normalized;
    }

    private static FocusActivationState? NormalizeFocusActivationState(
        FocusActivationState? state,
        IReadOnlySet<string> profileIds)
    {
        if (state is null)
        {
            return null;
        }

        var profileId = NormalizeIdentifier(state.ProfileId);
        if (profileId is null || !profileIds.Contains(profileId))
        {
            return null;
        }

        var source = EnumValue(state.Source, FocusActivationSource.Manual);
        var startedAtUtc = state.StartedAtUtc.ToUniversalTime();
        var endsAtUtc = state.EndsAtUtc?.ToUniversalTime();
        if (endsAtUtc <= startedAtUtc)
        {
            return null;
        }

        var startedUnchanged =
            startedAtUtc == state.StartedAtUtc &&
            state.StartedAtUtc.Offset == TimeSpan.Zero;
        var endUnchanged =
            endsAtUtc == state.EndsAtUtc &&
            (state.EndsAtUtc is null || state.EndsAtUtc.Value.Offset == TimeSpan.Zero);

        return profileId == state.ProfileId &&
               source == state.Source &&
               startedUnchanged &&
               endUnchanged
            ? state
            : state with
            {
                ProfileId = profileId,
                Source = source,
                StartedAtUtc = startedAtUtc,
                EndsAtUtc = endsAtUtc
            };
    }

    private static string? NormalizeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= 64 ? normalized : normalized[..64];
    }

    private static string? NormalizeCustomFocusName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length <= 40 ? normalized : normalized[..40];
    }

    private static string? NormalizeFocusRuleTarget(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = FocusApplicationTarget.Normalize(value);
        if (normalized.Length == 0)
        {
            return null;
        }

        return normalized.Length <= 260 ? normalized : normalized[..260];
    }

    private static ClockDisplaySettings NormalizeClock(ClockDisplaySettings? value)
    {
        var settings = value ?? ClockDisplaySettings.Default;
        return settings with
        {
            HourFormat = EnumValue(
                settings.HourFormat,
                ClockDisplaySettings.Default.HourFormat),
            DateFormat = EnumValue(
                settings.DateFormat,
                ClockDisplaySettings.Default.DateFormat)
        };
    }

    private static StoreUpdateSettings NormalizeStoreUpdates(StoreUpdateSettings? value)
    {
        var settings = value ?? StoreUpdateSettings.Default;
        return settings with
        {
            LastCheckUtc = settings.LastCheckUtc?.ToUniversalTime(),
            LastNotifiedVersion = NormalizeVersion(settings.LastNotifiedVersion)
        };
    }

    private static string? NormalizeVersion(string? value)
    {
        if (!Version.TryParse(value, out var version))
        {
            return null;
        }

        return new Version(
            version.Major,
            version.Minor,
            Math.Max(0, version.Build),
            Math.Max(0, version.Revision)).ToString(4);
    }

    private static GlobalHotKeySettings NormalizeHotKeys(GlobalHotKeySettings? value)
    {
        var settings = value ?? GlobalHotKeySettings.Default;
        var normalized = new Dictionary<HotKeyAction, HotKeyGestureSetting>();
        var occupied = new HashSet<HotKeyGestureSetting>();
        var changed = settings.Bindings is null;
        if (settings.Bindings is not null)
        {
            foreach (var pair in settings.Bindings)
            {
                if (!Enum.IsDefined(pair.Key) ||
                    !HotKeyGestureValidator.IsValid(pair.Value) ||
                    !occupied.Add(pair.Value))
                {
                    changed = true;
                    continue;
                }

                normalized[pair.Key] = pair.Value;
            }
        }

        return !changed && settings.Bindings is not null && normalized.Count == settings.Bindings.Count
            ? settings
            : new GlobalHotKeySettings(settings.IsEnabled, normalized);
    }

    private static IReadOnlyDictionary<string, ModuleSettingsEnvelope> NormalizeModules(
        IReadOnlyDictionary<string, ModuleSettingsEnvelope>? modules)
    {
        var normalized = new Dictionary<string, ModuleSettingsEnvelope>(StringComparer.Ordinal);
        var changed = modules is null;
        if (modules is not null)
        {
            foreach (var pair in modules)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null)
                {
                    changed = true;
                    continue;
                }

                var envelope = pair.Value;
                var normalizedKey = pair.Key.Trim();
                var normalizedEnvelope = envelope with
                {
                    SchemaVersion = Math.Max(1, envelope.SchemaVersion),
                    EventDurationSeconds = ClampFinite(envelope.EventDurationSeconds, 1, 60, 5),
                    Options = envelope.Options is null
                        ? new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal)
                        : envelope.Options
                };
                normalized[normalizedKey] = normalizedEnvelope;
                changed |= normalizedKey != pair.Key || normalizedEnvelope != envelope;
            }
        }

        if (normalized.TryAdd("media", ModuleSettingsEnvelope.MediaDefault))
        {
            changed = true;
        }

        if (normalized.TryAdd("privacy", ModuleSettingsEnvelope.PrivacyDefault))
        {
            changed = true;
        }

        if (normalized.TryAdd("system-activity", ModuleSettingsEnvelope.SystemActivityDefault))
        {
            changed = true;
        }

        if (normalized.TryAdd("volume", ModuleSettingsEnvelope.VolumeDefault))
        {
            changed = true;
        }
        else
        {
            var volume = normalized["volume"];
            System.Text.Json.JsonElement showName = default;
            var hasValidShowOutputDeviceName =
                volume.Options is not null &&
                volume.Options.TryGetValue("showOutputDeviceName", out showName) &&
                showName.ValueKind is System.Text.Json.JsonValueKind.True or
                    System.Text.Json.JsonValueKind.False;
            var duration = ClampFinite(
                volume.EventDurationSeconds,
                1,
                10,
                ModuleSettingsEnvelope.VolumeDefault.EventDurationSeconds);
            if (hasValidShowOutputDeviceName && duration == volume.EventDurationSeconds)
            {
                normalized["volume"] = volume;
            }
            else
            {
                var options = new Dictionary<string, System.Text.Json.JsonElement>(
                    volume.Options ?? new Dictionary<string, System.Text.Json.JsonElement>(),
                    StringComparer.Ordinal)
                {
                    ["showOutputDeviceName"] =
                        System.Text.Json.JsonSerializer.SerializeToElement(
                            hasValidShowOutputDeviceName
                                ? showName.GetBoolean()
                                : true)
                };
                normalized["volume"] = volume with
                {
                    EventDurationSeconds = duration,
                    Options = options
                };
                changed = true;
            }
        }

        if (normalized.TryAdd("battery", ModuleSettingsEnvelope.BatteryDefault)) changed = true;
        if (normalized.TryAdd("network", ModuleSettingsEnvelope.NetworkDefault)) changed = true;
        if (normalized.TryAdd("bluetooth", ModuleSettingsEnvelope.BluetoothDefault)) changed = true;
        if (normalized.TryAdd("timer", ModuleSettingsEnvelope.TimerDefault)) changed = true;
        if (normalized.TryAdd("notifications", ModuleSettingsEnvelope.NotificationsDefault)) changed = true;
        if (normalized.TryAdd("transfers", ModuleSettingsEnvelope.TransfersDefault)) changed = true;

        return !changed && modules is not null && normalized.Count == modules.Count
            ? modules
            : normalized;
    }

    private static OnboardingSettings NormalizeOnboarding(OnboardingSettings? value)
    {
        var settings = value ?? OnboardingSettings.Default;
        if (!settings.IsCompleted)
        {
            return OnboardingSettings.Default;
        }

        return settings with
        {
            CompletedVersion = Math.Clamp(settings.CompletedVersion, 1, OnboardingSettings.CurrentVersion),
            CompletedAtUtc = settings.CompletedAtUtc?.ToUniversalTime()
        };
    }

    private static FullscreenSettings NormalizeFullscreen(FullscreenSettings? value)
    {
        var settings = value ?? FullscreenSettings.Default;
        var behavior = EnumValue(settings.Behavior, FullscreenSettings.Default.Behavior);
        return settings with
        {
            Behavior = behavior,
            Enabled = behavior != FullscreenDockBehavior.HideCompletely,
            Style = EnumValue(settings.Style, FullscreenSettings.Default.Style),
            NotificationSeconds = ClampFinite(
                settings.NotificationSeconds,
                1,
                30,
                FullscreenSettings.Default.NotificationSeconds)
        };
    }

    private static double ClampFinite(double value, double minimum, double maximum, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, minimum, maximum) : fallback;

    private static T EnumValue<T>(T value, T fallback) where T : struct, Enum =>
        Enum.IsDefined(value) ? value : fallback;

    private static string NormalizeColor(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var color = value.Trim().ToUpperInvariant();
        if (color.Length == 7 && color[0] == '#' && color[1..].All(Uri.IsHexDigit))
        {
            return color;
        }

        return fallback;
    }

    private static MiaDockSettings Migrate(MiaDockSettings settings)
    {
        var general = settings.General ?? GeneralSettings.Default;
        if (settings.SchemaVersion < 9 && general.InteractionMode == IslandInteractionMode.Hover)
        {
            general = general with { InteractionMode = IslandInteractionMode.HoverAndClick };
        }
        if (settings.SchemaVersion < 12)
        {
            general = general with
            {
                PassiveModuleReturnSeconds = GeneralSettings.Default.PassiveModuleReturnSeconds
            };
        }
        if (settings.SchemaVersion < 14)
        {
            general = general with { Clock = ClockDisplaySettings.Default };
        }

        var focus = settings.SchemaVersion < 15
            ? FocusSettings.Default
            : settings.Focus;

        var appearance = settings.Appearance ?? AppearanceSettings.Default;
        if (settings.SchemaVersion < 2 && appearance.CollapsedWidth == 184 && appearance.CollapsedHeight == 40)
        {
            appearance = appearance with
            {
                CollapsedWidth = AppearanceSettings.Default.CollapsedWidth,
                CollapsedHeight = AppearanceSettings.Default.CollapsedHeight,
                CornerRadius = AppearanceSettings.Default.CornerRadius,
                NotificationWidth = appearance.NotificationWidth == 360
                    ? AppearanceSettings.Default.NotificationWidth
                    : appearance.NotificationWidth,
                BackgroundColor = appearance.BackgroundColor == "#050506"
                    ? AppearanceSettings.Default.BackgroundColor
                    : appearance.BackgroundColor,
                Opacity = appearance.Opacity == 0.98
                    ? AppearanceSettings.Default.Opacity
                    : appearance.Opacity
            };
        }

        if (settings.SchemaVersion < 19)
        {
            var legacyRadius = ClampFinite(
                appearance.CornerRadius,
                0,
                48,
                AppearanceSettings.Default.CornerRadius);
            appearance = appearance with
            {
                EdgeMargin = AppearanceSettings.Default.EdgeMargin,
                CornerRadii = DockCornerRadii.Uniform(legacyRadius),
                LinkCornerRadii = true
            };

            var legacyFullscreen = settings.Fullscreen ?? FullscreenSettings.Default;
            settings = settings with
            {
                Fullscreen = legacyFullscreen with
                {
                    Behavior = legacyFullscreen.Enabled
                        ? FullscreenDockBehavior.NotificationsOnly
                        : FullscreenDockBehavior.HideCompletely
                }
            };
        }

        if (settings.SchemaVersion < 20)
        {
            general = general with { ShowKeyboardLockEvents = true };
            settings = settings with { General = general };
        }

        if (settings.SchemaVersion < 22)
        {
            general = general with { ShowUsbDeviceEvents = true };
            settings = settings with { General = general };
        }

        var modules = settings.Modules;
        if (settings.SchemaVersion < 5 && (modules is null || modules.Count == 0))
        {
            modules = MiaDockSettings.Default.Modules;
        }

        return settings with
        {
            General = general,
            Appearance = appearance,
            Focus = focus,
            Modules = modules
        };
    }
}
