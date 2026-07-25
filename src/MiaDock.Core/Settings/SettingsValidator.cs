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
            PassiveModuleReturnSeconds = ClampFinite(
                general.PassiveModuleReturnSeconds,
                3,
                30,
                GeneralSettings.Default.PassiveModuleReturnSeconds)
        };

        var appearance = settings.Appearance ?? AppearanceSettings.Default;
        appearance = appearance with
        {
            Theme = EnumValue(appearance.Theme, AppearanceSettings.Default.Theme),
            CollapsedWidth = ClampFinite(appearance.CollapsedWidth, 120, 360, AppearanceSettings.Default.CollapsedWidth),
            CollapsedHeight = ClampFinite(appearance.CollapsedHeight, 32, 96, AppearanceSettings.Default.CollapsedHeight),
            HoverWidth = ClampFinite(appearance.HoverWidth, 180, 520, AppearanceSettings.Default.HoverWidth),
            HoverHeight = ClampFinite(appearance.HoverHeight, 48, 160, AppearanceSettings.Default.HoverHeight),
            ExpandedWidth = ClampFinite(appearance.ExpandedWidth, 320, 720, AppearanceSettings.Default.ExpandedWidth),
            ExpandedHeight = ClampFinite(appearance.ExpandedHeight, 260, 420, AppearanceSettings.Default.ExpandedHeight),
            NotificationWidth = ClampFinite(appearance.NotificationWidth, 240, 620, AppearanceSettings.Default.NotificationWidth),
            NotificationHeight = ClampFinite(appearance.NotificationHeight, 64, 180, AppearanceSettings.Default.NotificationHeight),
            CornerRadius = ClampFinite(appearance.CornerRadius, 0, 48, AppearanceSettings.Default.CornerRadius),
            BackgroundColor = NormalizeColor(appearance.BackgroundColor, AppearanceSettings.Default.BackgroundColor),
            AccentColor = NormalizeColor(appearance.AccentColor, AppearanceSettings.Default.AccentColor),
            Opacity = ClampFinite(appearance.Opacity, 0.35, 1, AppearanceSettings.Default.Opacity),
            ShadowIntensity = ClampFinite(appearance.ShadowIntensity, 0, 1, AppearanceSettings.Default.ShadowIntensity),
            AnimationSpeed = ClampFinite(appearance.AnimationSpeed, 0.5, 2, AppearanceSettings.Default.AnimationSpeed),
            AnimationKind = EnumValue(appearance.AnimationKind, AppearanceSettings.Default.AnimationKind)
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
            Modules = NormalizeModules(settings.Modules)
        };
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

        if (normalized.TryAdd("system-activity", ModuleSettingsEnvelope.SystemActivityDefault))
        {
            changed = true;
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
        return settings with
        {
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

        var modules = settings.Modules;
        if (settings.SchemaVersion < 5 && (modules is null || modules.Count == 0))
        {
            modules = MiaDockSettings.Default.Modules;
        }

        return settings with { General = general, Appearance = appearance, Modules = modules };
    }
}
