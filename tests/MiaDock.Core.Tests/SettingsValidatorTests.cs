using MiaDock.Core.Presentation;
using MiaDock.Core.Settings;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class SettingsValidatorTests
{
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
    public void Normalize_ClampsExpandedHeightToAccessibleModuleMinimum()
    {
        var undersized = MiaDockSettings.Default with
        {
            Appearance = MiaDockSettings.Default.Appearance with { ExpandedHeight = 120 }
        };

        var normalized = SettingsValidator.Normalize(undersized);

        Assert.AreEqual(260, normalized.Appearance.ExpandedHeight);
        Assert.IsGreaterThanOrEqualTo(260, AppearanceSettings.Default.ExpandedHeight);
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
        Assert.IsTrue(result.Modules["system-activity"].IsEnabled);
        Assert.AreEqual(3, result.Modules["system-activity"].EventDurationSeconds);
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
}
