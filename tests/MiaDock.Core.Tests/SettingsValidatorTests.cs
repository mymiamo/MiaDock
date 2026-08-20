using MiaDock.Core.Presentation;
using MiaDock.Core.Settings;
using System.Text.Json;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class SettingsValidatorTests
{
    [TestMethod]
    public void Normalize_SchemaTwentySevenWithoutAudibleNotifications_AddsEnabledDefaults()
    {
        var legacy = MiaDockSettings.Default with
        {
            SchemaVersion = 27,
            AudibleNotifications = null!
        };

        var result = SettingsValidator.Normalize(legacy);

        Assert.AreEqual(30, result.SchemaVersion);
        Assert.AreEqual(AudibleNotificationSettings.Default, result.AudibleNotifications);
        Assert.IsTrue(result.AudibleNotifications.IsEnabled);
        Assert.IsTrue(result.AudibleNotifications.NetworkOfflineEnabled);
        Assert.IsTrue(result.AudibleNotifications.ConnectedWithoutInternetEnabled);
        Assert.IsTrue(result.AudibleNotifications.LowBatteryEnabled);
        Assert.IsTrue(result.AudibleNotifications.DeviceConnectedEnabled);
        Assert.IsTrue(result.AudibleNotifications.DeviceDisconnectedEnabled);
        Assert.IsTrue(result.AudibleNotifications.HourlyEnabled);
        Assert.IsFalse(result.Modules["hourly-notification"].IsEnabled);
    }

    [TestMethod]
    public void Normalize_SchemaTwentyEight_AddsEnabledHourlySoundAndDisabledModule()
    {
        var modules = new Dictionary<string, ModuleSettingsEnvelope>(
            MiaDockSettings.Default.Modules,
            StringComparer.Ordinal);
        modules.Remove("hourly-notification");
        var legacy = MiaDockSettings.Default with
        {
            SchemaVersion = 28,
            AudibleNotifications = MiaDockSettings.Default.AudibleNotifications with
            {
                HourlyEnabled = false
            },
            Modules = modules
        };

        var result = SettingsValidator.Normalize(legacy);

        Assert.AreEqual(30, result.SchemaVersion);
        Assert.IsTrue(result.AudibleNotifications.HourlyEnabled);
        Assert.IsFalse(result.Modules["hourly-notification"].IsEnabled);
        Assert.AreEqual(4, result.Modules["hourly-notification"].EventDurationSeconds);
        Assert.IsFalse(result.Modules["hourly-notification"].ShowInFullscreen);
    }

    [TestMethod]
    public void Normalize_SchemaTwentyNine_PreservesHourlyPreferences()
    {
        var modules = new Dictionary<string, ModuleSettingsEnvelope>(
            MiaDockSettings.Default.Modules,
            StringComparer.Ordinal)
        {
            ["hourly-notification"] = ModuleSettingsEnvelope.HourlyNotificationDefault with
            {
                IsEnabled = true
            }
        };
        var settings = MiaDockSettings.Default with
        {
            AudibleNotifications = MiaDockSettings.Default.AudibleNotifications with
            {
                HourlyEnabled = false
            },
            Modules = modules
        };

        var result = SettingsValidator.Normalize(settings);

        Assert.IsFalse(result.AudibleNotifications.HourlyEnabled);
        Assert.IsTrue(result.Modules["hourly-notification"].IsEnabled);
    }

    [TestMethod]
    public void Normalize_SchemaTwentyNine_AddsAudioOutputDefaultsWithoutChangingPreferences()
    {
        var legacy = MiaDockSettings.Default with
        {
            SchemaVersion = 29,
            AudibleNotifications = MiaDockSettings.Default.AudibleNotifications with
            {
                OutputDeviceId = "  endpoint-id  ",
                VolumePercent = 135
            }
        };

        var result = SettingsValidator.Normalize(legacy);

        Assert.AreEqual(30, result.SchemaVersion);
        Assert.AreEqual("endpoint-id", result.AudibleNotifications.OutputDeviceId);
        Assert.AreEqual(100, result.AudibleNotifications.VolumePercent);
    }

    [TestMethod]
    public void AudibleNotificationMasterSwitch_PreservesIndividualPreferences()
    {
        var preferences = AudibleNotificationSettings.Default with
        {
            IsEnabled = false,
            DeviceDisconnectedEnabled = false
        };

        Assert.IsFalse(preferences.Allows(MiaDock.Core.Modules.AudibleNotificationCue.DeviceConnected));

        var enabledAgain = preferences with { IsEnabled = true };
        Assert.IsTrue(enabledAgain.DeviceConnectedEnabled);
        Assert.IsFalse(enabledAgain.DeviceDisconnectedEnabled);
        Assert.IsTrue(enabledAgain.HourlyEnabled);
        Assert.IsTrue(enabledAgain.Allows(MiaDock.Core.Modules.AudibleNotificationCue.DeviceConnected));
        Assert.IsFalse(enabledAgain.Allows(MiaDock.Core.Modules.AudibleNotificationCue.DeviceDisconnected));
        Assert.IsTrue(enabledAgain.Allows(MiaDock.Core.Modules.AudibleNotificationCue.Hourly));
    }

    [TestMethod]
    public void Normalize_SchemaEighteen_MigratesLegacyFullscreenToggle()
    {
        var disabled = MiaDockSettings.Default with
        {
            SchemaVersion = 18,
            Fullscreen = MiaDockSettings.Default.Fullscreen with
            {
                Enabled = false,
                Behavior = FullscreenDockBehavior.NotificationsOnly
            }
        };

        var result = SettingsValidator.Normalize(disabled);

        Assert.AreEqual(FullscreenDockBehavior.HideCompletely, result.Fullscreen.Behavior);
        Assert.IsFalse(result.Fullscreen.Enabled);
    }

    [TestMethod]
    public void Normalize_FullscreenBehaviorKeepsLegacyEnabledFieldConsistent()
    {
        var source = MiaDockSettings.Default with
        {
            Fullscreen = MiaDockSettings.Default.Fullscreen with
            {
                Enabled = false,
                Behavior = FullscreenDockBehavior.EdgeReveal
            }
        };

        var result = SettingsValidator.Normalize(source);

        Assert.AreEqual(FullscreenDockBehavior.EdgeReveal, result.Fullscreen.Behavior);
        Assert.IsTrue(result.Fullscreen.Enabled);
    }
    [TestMethod]
    public void Normalize_ValidSettings_RemainsValueEqual()
    {
        var result = SettingsValidator.Normalize(MiaDockSettings.Default);

        Assert.AreEqual(MiaDockSettings.Default, result);
    }

    [TestMethod]
    public void Normalize_PreservesEnglishLanguagePreference()
    {
        var settings = MiaDockSettings.Default with
        {
            General = MiaDockSettings.Default.General with { Language = AppLanguage.English }
        };

        var result = SettingsValidator.Normalize(settings);

        Assert.AreEqual(AppLanguage.English, result.General.Language);
        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, result.SchemaVersion);
    }

    [TestMethod]
    public void Normalize_PreservesEdgeRevealVisibilityMode()
    {
        var settings = MiaDockSettings.Default with
        {
            General = MiaDockSettings.Default.General with
            {
                VisibilityMode = IslandVisibilityMode.EdgeReveal
            }
        };

        var result = SettingsValidator.Normalize(settings);

        Assert.AreEqual(IslandVisibilityMode.EdgeReveal, result.General.VisibilityMode);
    }

    [TestMethod]
    public void Normalize_ClampsDimensionsAndRepairsColors()
    {
        var invalid = MiaDockSettings.Default with
        {
            Appearance = MiaDockSettings.Default.Appearance with
            {
                CollapsedWidth = -20,
                ExpandedHeight = double.PositiveInfinity,
                Opacity = 5,
                AnimationSpeed = 0,
                BackgroundColor = "not-a-color",
                AccentColor = "#aBc123"
            },
            Fullscreen = MiaDockSettings.Default.Fullscreen with { NotificationSeconds = 80 }
        };

        var result = SettingsValidator.Normalize(invalid);

        Assert.AreEqual(120, result.Appearance.CollapsedWidth);
        Assert.AreEqual(AppearanceSettings.Default.ExpandedHeight, result.Appearance.ExpandedHeight);
        Assert.AreEqual(1, result.Appearance.Opacity);
        Assert.AreEqual(0.5, result.Appearance.AnimationSpeed);
        Assert.AreEqual(AppearanceSettings.Default.BackgroundColor, result.Appearance.BackgroundColor);
        Assert.AreEqual("#ABC123", result.Appearance.AccentColor);
        Assert.AreEqual(30, result.Fullscreen.NotificationSeconds);
    }

    [TestMethod]
    public void Normalize_RepairsUnknownEnumValues()
    {
        var invalid = MiaDockSettings.Default with
        {
            General = MiaDockSettings.Default.General with
            {
                Language = (AppLanguage)999,
                InteractionMode = (IslandInteractionMode)999
            },
            Appearance = MiaDockSettings.Default.Appearance with
            {
                AnimationKind = (IslandAnimationKind)999
            }
        };

        var result = SettingsValidator.Normalize(invalid);

        Assert.AreEqual(GeneralSettings.Default.InteractionMode, result.General.InteractionMode);
        Assert.AreEqual(GeneralSettings.Default.Language, result.General.Language);
        Assert.AreEqual(AppearanceSettings.Default.AnimationKind, result.Appearance.AnimationKind);
    }

    [TestMethod]
    public void Normalize_AllowsReadableTransparencyAndClampsLowerValues()
    {
        var readable = MiaDockSettings.Default with
        {
            Appearance = MiaDockSettings.Default.Appearance with { Opacity = 0.42 }
        };
        var tooTransparent = MiaDockSettings.Default with
        {
            Appearance = MiaDockSettings.Default.Appearance with { Opacity = 0.1 }
        };

        Assert.AreEqual(0.42, SettingsValidator.Normalize(readable).Appearance.Opacity, 0.001);
        Assert.AreEqual(0.35, SettingsValidator.Normalize(tooTransparent).Appearance.Opacity, 0.001);
    }

    [TestMethod]
    public void Normalize_ClampsPassiveModuleReturnDelay()
    {
        var tooShort = MiaDockSettings.Default with
        {
            General = MiaDockSettings.Default.General with { PassiveModuleReturnSeconds = 1 }
        };
        var tooLong = MiaDockSettings.Default with
        {
            General = MiaDockSettings.Default.General with { PassiveModuleReturnSeconds = 90 }
        };

        Assert.AreEqual(3, SettingsValidator.Normalize(tooShort).General.PassiveModuleReturnSeconds);
        Assert.AreEqual(30, SettingsValidator.Normalize(tooLong).General.PassiveModuleReturnSeconds);
    }

    [TestMethod]
    public void Normalize_PreviousSchema_UsesDefaultPassiveModuleReturnDelay()
    {
        var old = MiaDockSettings.Default with
        {
            SchemaVersion = 11,
            General = MiaDockSettings.Default.General with { PassiveModuleReturnSeconds = 0 }
        };

        var normalized = SettingsValidator.Normalize(old);

        Assert.AreEqual(8, normalized.General.PassiveModuleReturnSeconds);
        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, normalized.SchemaVersion);
    }

    [TestMethod]
    public void Normalize_SchemaThirteen_UsesDefaultClockDisplaySettings()
    {
        var old = MiaDockSettings.Default with
        {
            SchemaVersion = 13,
            General = MiaDockSettings.Default.General with
            {
                Clock = new ClockDisplaySettings(
                    ClockHourFormat.TwelveHour,
                    ShowSeconds: true,
                    ShowDate: false,
                    ClockDateFormat.Long,
                    ShowWeekday: false)
            }
        };

        var normalized = SettingsValidator.Normalize(old);

        Assert.AreEqual(ClockDisplaySettings.Default, normalized.General.Clock);
        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, normalized.SchemaVersion);
    }

    [TestMethod]
    public void Normalize_RepairsUnknownClockFormatValues()
    {
        var invalid = MiaDockSettings.Default with
        {
            General = MiaDockSettings.Default.General with
            {
                Clock = ClockDisplaySettings.Default with
                {
                    HourFormat = (ClockHourFormat)99,
                    DateFormat = (ClockDateFormat)99
                }
            }
        };

        var normalized = SettingsValidator.Normalize(invalid);

        Assert.AreEqual(ClockDisplaySettings.Default.HourFormat, normalized.General.Clock.HourFormat);
        Assert.AreEqual(ClockDisplaySettings.Default.DateFormat, normalized.General.Clock.DateFormat);
    }

    [TestMethod]
    public void Normalize_ClampsExpandedSizeToDashboardMinimum()
    {
        var undersized = MiaDockSettings.Default with
        {
            Appearance = MiaDockSettings.Default.Appearance with
            {
                ExpandedWidth = 320,
                ExpandedHeight = 120
            }
        };

        var normalized = SettingsValidator.Normalize(undersized);

        Assert.AreEqual(548, normalized.Appearance.ExpandedWidth);
        Assert.AreEqual(360, normalized.Appearance.ExpandedHeight);
        Assert.IsGreaterThanOrEqualTo(360, AppearanceSettings.Default.ExpandedHeight);
    }

    [TestMethod]
    public void Normalize_VersionOneDefaults_MigratesReferenceCapsuleDimensions()
    {
        var old = MiaDockSettings.Default with
        {
            SchemaVersion = 1,
            Appearance = MiaDockSettings.Default.Appearance with
            {
                CollapsedWidth = 184,
                CollapsedHeight = 40,
                NotificationWidth = 360,
                CornerRadius = 22,
                BackgroundColor = "#050506",
                Opacity = 0.98
            }
        };

        var result = SettingsValidator.Normalize(old);

        Assert.AreEqual(292, result.Appearance.CollapsedWidth);
        Assert.AreEqual(46, result.Appearance.CollapsedHeight);
        Assert.AreEqual(23, result.Appearance.CornerRadius);
        Assert.AreEqual("#000000", result.Appearance.BackgroundColor);
        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, result.SchemaVersion);
    }

    [TestMethod]
    public void Normalize_SilentTrayMode_AlwaysKeepsTrayIconReachable()
    {
        var invalid = MiaDockSettings.Default with
        {
            Tray = MiaDockSettings.Default.Tray with { ShowIcon = false },
            StartupShutdown = MiaDockSettings.Default.StartupShutdown with
            {
                LaunchMode = StartupLaunchMode.SilentTray
            }
        };

        var result = SettingsValidator.Normalize(invalid);

        Assert.IsTrue(result.Tray.ShowIcon);
        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, result.SchemaVersion);
    }

    [TestMethod]
    public void DefaultCloseBehavior_RequiresFirstCloseConfirmation()
    {
        Assert.AreEqual(CloseBehaviorSetting.MinimizeToTray, StartupShutdownSettings.Default.CloseBehavior);
        Assert.IsFalse(StartupShutdownSettings.Default.HasConfirmedCloseBehavior);
    }

    [TestMethod]
    public void DefaultInteraction_SupportsBothHoverAndClick()
    {
        Assert.AreEqual(IslandInteractionMode.HoverAndClick, GeneralSettings.Default.InteractionMode);
    }

    [TestMethod]
    public void Normalize_PreviousDefaultInteraction_MigratesToHoverAndClick()
    {
        var previous = MiaDockSettings.Default with
        {
            SchemaVersion = 8,
            General = MiaDockSettings.Default.General with { InteractionMode = IslandInteractionMode.Hover }
        };

        var result = SettingsValidator.Normalize(previous);

        Assert.AreEqual(IslandInteractionMode.HoverAndClick, result.General.InteractionMode);
        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, result.SchemaVersion);
    }

    [TestMethod]
    public void DefaultOnboarding_IsIncomplete()
    {
        Assert.IsFalse(MiaDockSettings.Default.Onboarding.IsCompleted);
        Assert.AreEqual(0, MiaDockSettings.Default.Onboarding.CompletedVersion);
    }

    [TestMethod]
    public void Normalize_CompletedOnboarding_RepairsVersionAndTimestamp()
    {
        var localTimestamp = new DateTimeOffset(2026, 7, 22, 12, 0, 0, TimeSpan.FromHours(3));
        var settings = MiaDockSettings.Default with
        {
            Onboarding = new OnboardingSettings(true, 999, localTimestamp)
        };

        var result = SettingsValidator.Normalize(settings);

        Assert.IsTrue(result.Onboarding.IsCompleted);
        Assert.AreEqual(OnboardingSettings.CurrentVersion, result.Onboarding.CompletedVersion);
        Assert.AreEqual(TimeSpan.Zero, result.Onboarding.CompletedAtUtc?.Offset);
    }

    [TestMethod]
    public void Normalize_ModuleEnvelope_RepairsOnlyInvalidModuleValues()
    {
        var settings = MiaDockSettings.Default with
        {
            Modules = new Dictionary<string, ModuleSettingsEnvelope>
            {
                ["media"] = ModuleSettingsEnvelope.MediaDefault,
                ["timer"] = new(0, true, 500, false, null)
            }
        };

        var result = SettingsValidator.Normalize(settings);

        Assert.AreEqual(1, result.Modules["timer"].SchemaVersion);
        Assert.AreEqual(60, result.Modules["timer"].EventDurationSeconds);
        Assert.IsFalse(result.Modules["timer"].ShowInFullscreen);
        Assert.AreEqual(5, result.Modules["media"].EventDurationSeconds);
        Assert.IsTrue(result.Modules["privacy"].IsEnabled);
        Assert.AreEqual(3.5, result.Modules["privacy"].EventDurationSeconds);
        Assert.IsTrue(result.Modules["system-activity"].IsEnabled);
        Assert.AreEqual(3, result.Modules["system-activity"].EventDurationSeconds);
        Assert.IsTrue(result.Modules["volume"].IsEnabled);
        Assert.AreEqual(2.5, result.Modules["volume"].EventDurationSeconds);
        Assert.IsTrue(result.Modules["volume"].Options!["showOutputDeviceName"].GetBoolean());
    }

    [TestMethod]
    public void Normalize_VolumeModule_ClampsDurationAndRepairsDeviceNameOption()
    {
        var modules = new Dictionary<string, ModuleSettingsEnvelope>(
            MiaDockSettings.Default.Modules,
            StringComparer.Ordinal)
        {
            ["volume"] = new(
                1,
                true,
                45,
                false,
                new Dictionary<string, System.Text.Json.JsonElement>
                {
                    ["showOutputDeviceName"] =
                        System.Text.Json.JsonSerializer.SerializeToElement("invalid")
                })
        };

        var result = SettingsValidator.Normalize(
            MiaDockSettings.Default with { Modules = modules });

        Assert.AreEqual(10, result.Modules["volume"].EventDurationSeconds);
        Assert.IsFalse(result.Modules["volume"].ShowInFullscreen);
        Assert.IsTrue(result.Modules["volume"].Options!["showOutputDeviceName"].GetBoolean());
    }

    [TestMethod]
    public void SensitiveContent_IsOptInByDefault()
    {
        Assert.IsFalse(MiaDockSettings.Default.Privacy.ShowSensitiveContentInFullscreen);
        Assert.IsFalse(MiaDockSettings.Default.Privacy.ShowSensitiveContentWhenLocked);
    }

    [TestMethod]
    public void Normalize_MissingPrivacySettings_UsesSafeDefaults()
    {
        var settings = MiaDockSettings.Default with { Privacy = null! };

        var result = SettingsValidator.Normalize(settings);

        Assert.AreEqual(PresentationPrivacySettings.Default, result.Privacy);
    }

    [TestMethod]
    public void Normalize_MissingStoreUpdateSettings_EnablesAutomaticChecks()
    {
        var settings = MiaDockSettings.Default with
        {
            SchemaVersion = 12,
            StoreUpdates = null!
        };

        var result = SettingsValidator.Normalize(settings);

        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.IsTrue(result.StoreUpdates.AutomaticChecksEnabled);
        Assert.IsNull(result.StoreUpdates.LastCheckUtc);
        Assert.IsNull(result.StoreUpdates.LastNotifiedVersion);
    }

    [TestMethod]
    public void Normalize_StoreUpdateSettings_UsesUtcAndCanonicalVersion()
    {
        var localTimestamp = new DateTimeOffset(2026, 7, 27, 12, 0, 0, TimeSpan.FromHours(3));
        var settings = MiaDockSettings.Default with
        {
            StoreUpdates = new StoreUpdateSettings(false, localTimestamp, "1.2")
        };

        var result = SettingsValidator.Normalize(settings);

        Assert.IsFalse(result.StoreUpdates.AutomaticChecksEnabled);
        Assert.AreEqual(TimeSpan.Zero, result.StoreUpdates.LastCheckUtc?.Offset);
        Assert.AreEqual("1.2.0.0", result.StoreUpdates.LastNotifiedVersion);
    }

    [TestMethod]
    public void Normalize_SchemaFourteen_AddsDefaultFocusSettingsWithoutChangingOtherPreferences()
    {
        var previous = MiaDockSettings.Default with
        {
            SchemaVersion = 14,
            General = MiaDockSettings.Default.General with { Language = AppLanguage.English },
            Appearance = MiaDockSettings.Default.Appearance with { AccentColor = "#123456" },
            Focus = null!
        };

        var result = SettingsValidator.Normalize(previous);

        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.AreEqual(AppLanguage.English, result.General.Language);
        Assert.AreEqual("#123456", result.Appearance.AccentColor);
        Assert.AreEqual(FocusSettings.Default, result.Focus);
    }

    [TestMethod]
    public void Normalize_SchemaSeventeen_MigratesLegacyMotionWithoutLosingAppearance()
    {
        var previous = MiaDockSettings.Default with
        {
            SchemaVersion = 17,
            Appearance = MiaDockSettings.Default.Appearance with
            {
                AccentColor = "#123456",
                AnimationSpeed = 1.4,
                AnimationKind = IslandAnimationKind.SlideFade,
                Motion = null
            }
        };

        var result = SettingsValidator.Normalize(previous);

        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.AreEqual("#123456", result.Appearance.AccentColor);
        Assert.IsNotNull(result.Appearance.Motion);
        Assert.AreEqual(MotionPreset.Fluid, result.Appearance.Motion.Preset);
        Assert.AreEqual(1.4, result.Appearance.Motion.Speed, 0.001);
        Assert.IsTrue(result.General.ShowKeyboardLockEvents);
        Assert.IsTrue(result.General.ShowUsbDeviceEvents);
    }

    [TestMethod]
    public void Normalize_SchemaBelow22_EnablesUsbDeviceEventsByDefault()
    {
        var previous = MiaDockSettings.Default with
        {
            SchemaVersion = 21,
            General = MiaDockSettings.Default.General with { ShowUsbDeviceEvents = false }
        };

        // Schema < 22 always restores the new default (on) once.
        var result = SettingsValidator.Normalize(previous);

        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.IsTrue(result.General.ShowUsbDeviceEvents);
    }

    [TestMethod]
    public void Normalize_ClampsAdvancedMotionSettings()
    {
        var invalid = MiaDockSettings.Default with
        {
            Appearance = MiaDockSettings.Default.Appearance with
            {
                Motion = new MotionSettings(
                    (MotionPreset)999,
                    double.PositiveInfinity,
                    -4,
                    8,
                    900,
                    true,
                    true)
            }
        };

        var motion = SettingsValidator.Normalize(invalid).Appearance.Motion!;

        Assert.AreEqual(MotionPreset.Balanced, motion.Preset);
        Assert.AreEqual(MotionSettings.Default.Speed, motion.Speed);
        Assert.AreEqual(0, motion.Intensity);
        Assert.AreEqual(1, motion.Springiness);
        Assert.AreEqual(120, motion.ContentDelayMilliseconds);
        Assert.IsTrue(motion.EnableParallax);
        Assert.IsTrue(motion.EnableTransientBlur);
    }

    [TestMethod]
    public void Normalize_SchemaEighteen_MigratesLegacyRadiusAndEdgeMargin()
    {
        var previous = MiaDockSettings.Default with
        {
            SchemaVersion = 18,
            Appearance = MiaDockSettings.Default.Appearance with
            {
                CornerRadius = 17,
                EdgeMargin = 77,
                CornerRadii = null,
                LinkCornerRadii = false
            }
        };

        var result = SettingsValidator.Normalize(previous);

        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.AreEqual(12, result.Appearance.EdgeMargin);
        Assert.AreEqual(MiaDock.Core.Presentation.DockCornerRadii.Uniform(17), result.Appearance.EffectiveCornerRadii);
        Assert.IsTrue(result.Appearance.LinkCornerRadii);
    }

    [TestMethod]
    public void Normalize_ClampsIndependentCornerRadiiAndEdgeMargin()
    {
        var invalid = MiaDockSettings.Default with
        {
            Appearance = MiaDockSettings.Default.Appearance with
            {
                EdgeMargin = -20,
                LinkCornerRadii = false,
                CornerRadii = new(-3, 12, 90, double.NaN)
            }
        };

        var appearance = SettingsValidator.Normalize(invalid).Appearance;

        Assert.AreEqual(0, appearance.EdgeMargin);
        Assert.AreEqual(new MiaDock.Core.Presentation.DockCornerRadii(0, 12, 48, 23), appearance.EffectiveCornerRadii);
    }

    [TestMethod]
    public void Normalize_LinkedCornersUseTopLeftAndRemainIdempotent()
    {
        var settings = MiaDockSettings.Default with
        {
            Appearance = MiaDockSettings.Default.Appearance with
            {
                LinkCornerRadii = true,
                CornerRadii = new(9, 12, 18, 24)
            }
        };

        var once = SettingsValidator.Normalize(settings);
        var twice = SettingsValidator.Normalize(once);

        Assert.AreEqual(MiaDock.Core.Presentation.DockCornerRadii.Uniform(9), once.Appearance.EffectiveCornerRadii);
        Assert.AreEqual(once, twice);
    }

    [TestMethod]
    public void Normalize_SchemaTwentyFive_MigratesLegacyUsbEventPreferenceToDeviceHub()
    {
        var deviceHub = ModuleSettingsEnvelope.DeviceHubDefault;
        var options = deviceHub.Options!
            .Where(pair => pair.Key != "showStorageEvents")
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        var modules = new Dictionary<string, ModuleSettingsEnvelope>(MiaDockSettings.Default.Modules, StringComparer.Ordinal)
        {
            ["device-hub"] = deviceHub with { Options = options }
        };
        var previous = MiaDockSettings.Default with
        {
            SchemaVersion = 25,
            General = MiaDockSettings.Default.General with { ShowUsbDeviceEvents = false },
            Modules = modules
        };

        var result = SettingsValidator.Normalize(previous);
        var migrated = result.Modules["device-hub"].Options!["showStorageEvents"];

        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.IsFalse(migrated.GetBoolean());
    }

    [TestMethod]
    public void Normalize_SchemaTwentySix_MigratesClipboardPeekToSessionOnlySchema()
    {
        var clipboard = ModuleSettingsEnvelope.ClipboardPeekDefault with
        {
            IsEnabled = true,
            Options = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["historyLimit"] = JsonSerializer.SerializeToElement(15),
                ["eventMode"] = JsonSerializer.SerializeToElement("everything"),
                ["hideSensitiveContent"] = JsonSerializer.SerializeToElement(false),
                ["clearHistoryOnExit"] = JsonSerializer.SerializeToElement(false)
            }
        };
        var modules = new Dictionary<string, ModuleSettingsEnvelope>(MiaDockSettings.Default.Modules, StringComparer.Ordinal)
        {
            ["clipboard-peek"] = clipboard
        };

        var result = SettingsValidator.Normalize(MiaDockSettings.Default with
        {
            SchemaVersion = 26,
            Modules = modules
        });
        var migrated = result.Modules["clipboard-peek"];

        Assert.IsTrue(migrated.IsEnabled);
        Assert.IsFalse(migrated.ShowInFullscreen);
        Assert.AreEqual(20, migrated.Options!["historyLimit"].GetInt32());
        Assert.AreEqual("everything", migrated.Options["eventMode"].GetString());
        Assert.IsTrue(migrated.Options["showImageEvents"].GetBoolean());
        Assert.AreEqual(3, migrated.Options.Count);
        Assert.IsFalse(migrated.Options.ContainsKey("hideSensitiveContent"));
        Assert.IsFalse(migrated.Options.ContainsKey("clearHistoryOnExit"));
    }
}
