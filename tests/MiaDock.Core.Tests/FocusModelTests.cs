using MiaDock.Core.Focus;
using MiaDock.Core.Modules;
using MiaDock.Core.Settings;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class FocusModelTests
{
    [TestMethod]
    public void Default_ContainsFourStableBuiltInProfiles()
    {
        var profiles = FocusSettings.Default.Profiles;

        Assert.HasCount(4, profiles);
        Assert.AreEqual(4, profiles.Select(profile => profile.Id).Distinct(StringComparer.Ordinal).Count());
        CollectionAssert.AreEquivalent(
            new[]
            {
                FocusProfileDefaults.WorkId,
                FocusProfileDefaults.GamingId,
                FocusProfileDefaults.SleepId,
                FocusProfileDefaults.DoNotDisturbId
            },
            profiles.Select(profile => profile.Id).ToArray());
        Assert.IsTrue(profiles.All(profile => profile.CustomName is null));
        Assert.IsTrue(profiles.All(profile =>
            !string.IsNullOrWhiteSpace(FocusProfileDefaults.GetDisplayNameKey(profile))));
        Assert.IsTrue(FocusSettings.Default.IsEnabled);
    }

    [TestMethod]
    public void Normalize_DisabledFocusClearsActiveEffectButPreservesProfiles()
    {
        var custom = CreateCustom("custom", "Korunan profil");
        var source = MiaDockSettings.Default with
        {
            Focus = new FocusSettings(
                4,
                FocusProfileDefaults.All.Append(custom).ToArray(),
                new FocusActivationState(
                    custom.Id,
                    FocusActivationSource.Manual,
                    DateTimeOffset.Parse("2026-08-09T10:00:00Z"),
                    null),
                false)
        };

        var result = SettingsValidator.Normalize(source);

        Assert.IsFalse(result.Focus.IsEnabled);
        Assert.IsNull(result.Focus.ActiveState);
        Assert.IsTrue(result.Focus.Profiles.Any(profile => profile.Id == custom.Id));
        Assert.AreEqual(result.Focus, SettingsValidator.Normalize(result).Focus);
    }

    [TestMethod]
    public void DoNotDisturb_DefaultKeepsDockAvailableThroughGlobalVisibility()
    {
        var profile = FocusProfileDefaults.ForKind(FocusProfileKind.DoNotDisturb);

        Assert.AreEqual(
            FocusDockVisibility.UseGlobalSetting,
            profile.Behavior.DockVisibility);
    }

    [TestMethod]
    public void Normalize_MigratesLegacyDoNotDisturbVisibilityWithoutLosingActiveState()
    {
        var current = FocusProfileDefaults.ForKind(FocusProfileKind.DoNotDisturb);
        var legacy = current with
        {
            Behavior = current.Behavior with
            {
                DockVisibility = FocusDockVisibility.EventsOnly
            }
        };
        var active = new FocusActivationState(
            legacy.Id,
            FocusActivationSource.Manual,
            DateTimeOffset.Parse("2026-07-30T10:00:00Z"),
            null);
        var settings = MiaDockSettings.Default with
        {
            Focus = new FocusSettings(
                2,
                FocusProfileDefaults.All
                    .Select(profile => profile.Id == legacy.Id ? legacy : profile)
                    .ToArray(),
                active)
        };

        var result = SettingsValidator.Normalize(settings);
        var migrated = result.Focus.Profiles.Single(profile =>
            profile.Id == FocusProfileDefaults.DoNotDisturbId);

        Assert.AreEqual(4, result.Focus.SchemaVersion);
        Assert.AreEqual(
            FocusDockVisibility.UseGlobalSetting,
            migrated.Behavior.DockVisibility);
        Assert.AreEqual(active, result.Focus.ActiveState);
    }

    [TestMethod]
    public void Normalize_PreservesCustomizedDoNotDisturbVisibility()
    {
        var current = FocusProfileDefaults.ForKind(FocusProfileKind.DoNotDisturb);
        var customized = current with
        {
            Behavior = current.Behavior with
            {
                DockVisibility = FocusDockVisibility.EventsOnly,
                AllowFullscreenNotifications = true
            }
        };
        var settings = MiaDockSettings.Default with
        {
            Focus = new FocusSettings(
                2,
                FocusProfileDefaults.All
                    .Select(profile => profile.Id == customized.Id ? customized : profile)
                    .ToArray(),
                null)
        };

        var result = SettingsValidator.Normalize(settings);
        var preserved = result.Focus.Profiles.Single(profile =>
            profile.Id == FocusProfileDefaults.DoNotDisturbId);

        Assert.AreEqual(FocusDockVisibility.EventsOnly, preserved.Behavior.DockVisibility);
        Assert.IsTrue(preserved.Behavior.AllowFullscreenNotifications);
    }

    [TestMethod]
    public void Normalize_RepairsProfileLimitsAndNestedValues()
    {
        var invalid = CreateCustom(
            id: " custom ",
            name: new string('A', 45)) with
        {
            IconKey = "unknown",
            Color = "transparent",
            DefaultDurationMinutes = 5000,
            Behavior = FocusProfileBehavior.Default with
            {
                DockVisibility = (FocusDockVisibility)999,
                MinimumEventPriority = (ModuleEventPriority)999,
                AllowedModuleIds = [" media ", "media", "", "timer"]
            },
            Schedules =
            [
                new FocusSchedule(" schedule ", true, FocusDays.Monday | (FocusDays)(1 << 12), -20, 2000),
                new FocusSchedule("schedule", true, FocusDays.None, 60, 120)
            ],
            ActivationRules =
            [
                new FocusActivationRule(
                    " app ",
                    true,
                    FocusActivationRuleKind.ApplicationForeground,
                    " spotify.exe "),
                new FocusActivationRule(
                    "missing-target",
                    true,
                    FocusActivationRuleKind.ApplicationRunning,
                    null)
            ]
        };
        var settings = MiaDockSettings.Default with
        {
            Focus = new FocusSettings(0, [invalid], null)
        };

        var result = SettingsValidator.Normalize(settings);
        var custom = result.Focus.Profiles.Single(profile => profile.Id == "custom");

        Assert.HasCount(5, result.Focus.Profiles);
        Assert.AreEqual(FocusSettings.CurrentSchemaVersion, result.Focus.SchemaVersion);
        Assert.HasCount(40, custom.CustomName!);
        Assert.AreEqual("star", custom.IconKey);
        Assert.AreEqual("#0EA5E9", custom.Color);
        Assert.AreEqual(1440, custom.DefaultDurationMinutes);
        Assert.AreEqual(FocusDockVisibility.UseGlobalSetting, custom.Behavior.DockVisibility);
        Assert.AreEqual(ModuleEventPriority.Low, custom.Behavior.MinimumEventPriority);
        CollectionAssert.AreEqual(new[] { "media", "timer" }, custom.Behavior.AllowedModuleIds.ToArray());
        Assert.HasCount(1, custom.Schedules);
        Assert.AreEqual(FocusDays.Monday, custom.Schedules[0].Days);
        Assert.AreEqual(0, custom.Schedules[0].StartMinute);
        Assert.AreEqual(1439, custom.Schedules[0].EndMinute);
        Assert.HasCount(1, custom.ActivationRules);
        Assert.AreEqual("app", custom.ActivationRules[0].Id);
        Assert.AreEqual("spotify.exe", custom.ActivationRules[0].Target);
    }

    [TestMethod]
    public void Normalize_RemovesDuplicateIdsAndCapsProfilesAtSixteen()
    {
        var customProfiles = Enumerable.Range(0, 20)
            .Select(index => CreateCustom($"custom-{index}", $"Profile {index}"))
            .ToList();
        customProfiles.Insert(1, CreateCustom("custom-0", "Duplicate"));
        var settings = MiaDockSettings.Default with
        {
            Focus = new FocusSettings(1, customProfiles, null)
        };

        var result = SettingsValidator.Normalize(settings);

        Assert.HasCount(16, result.Focus.Profiles);
        Assert.AreEqual(
            result.Focus.Profiles.Count,
            result.Focus.Profiles.Select(profile => profile.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.IsTrue(FocusProfileDefaults.BuiltInIds.All(
            id => result.Focus.Profiles.Any(profile => profile.Id == id)));
    }

    [TestMethod]
    public void Normalize_UsesUtcForValidActiveState()
    {
        var started = new DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.FromHours(3));
        var settings = MiaDockSettings.Default with
        {
            Focus = FocusSettings.Default with
            {
                ActiveState = new FocusActivationState(
                    FocusProfileDefaults.WorkId,
                    FocusActivationSource.Manual,
                    started,
                    started.AddMinutes(25))
            }
        };

        var result = SettingsValidator.Normalize(settings);

        Assert.IsNotNull(result.Focus.ActiveState);
        Assert.AreEqual(TimeSpan.Zero, result.Focus.ActiveState.StartedAtUtc.Offset);
        Assert.AreEqual(TimeSpan.Zero, result.Focus.ActiveState.EndsAtUtc?.Offset);
    }

    [TestMethod]
    public void Normalize_ClearsOrphanAndMalformedActiveStates()
    {
        var now = DateTimeOffset.UtcNow;
        var orphan = MiaDockSettings.Default with
        {
            Focus = FocusSettings.Default with
            {
                ActiveState = new FocusActivationState(
                    "missing",
                    FocusActivationSource.Manual,
                    now,
                    now.AddHours(1))
            }
        };
        var malformed = MiaDockSettings.Default with
        {
            Focus = FocusSettings.Default with
            {
                ActiveState = new FocusActivationState(
                    FocusProfileDefaults.WorkId,
                    FocusActivationSource.Manual,
                    now,
                    now.AddMinutes(-1))
            }
        };

        Assert.IsNull(SettingsValidator.Normalize(orphan).Focus.ActiveState);
        Assert.IsNull(SettingsValidator.Normalize(malformed).Focus.ActiveState);
    }

    [TestMethod]
    public void PrivacyBehavior_CanRestrictButCannotLoosenGlobalPermission()
    {
        var permissive = FocusProfileBehavior.Default;
        var restrictive = permissive with
        {
            AllowSensitiveContentInFullscreen = false,
            AllowSensitiveContentWhenLocked = false
        };

        Assert.IsFalse(permissive.CanShowSensitiveContentInFullscreen(globalPermission: false));
        Assert.IsFalse(permissive.CanShowSensitiveContentWhenLocked(globalPermission: false));
        Assert.IsFalse(restrictive.CanShowSensitiveContentInFullscreen(globalPermission: true));
        Assert.IsFalse(restrictive.CanShowSensitiveContentWhenLocked(globalPermission: true));
        Assert.IsTrue(permissive.CanShowSensitiveContentInFullscreen(globalPermission: true));
    }

    private static FocusProfile CreateCustom(string id, string name) =>
        new(
            id,
            FocusProfileKind.Custom,
            name,
            "star",
            "#0EA5E9",
            null,
            FocusProfileBehavior.Default,
            Array.Empty<FocusSchedule>(),
            Array.Empty<FocusActivationRule>());
}
