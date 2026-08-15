using MiaDock.Core.Settings;
using MiaDock.Core.Focus;
using MiaDock.Platform.Windows.Settings;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MiaDock.Platform.Windows.Tests.Settings;

[TestClass]
public sealed class JsonSettingsStoreTests
{
    private string _directory = null!;
    private string _settingsPath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(Path.GetTempPath(), "MiaDockTests", Guid.NewGuid().ToString("N"));
        _settingsPath = Path.Combine(_directory, "settings.json");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveAndLoad_RoundTripsNormalizedSettings()
    {
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));
        var expected = MiaDockSettings.Default with
        {
            Appearance = MiaDockSettings.Default.Appearance with
            {
                CollapsedWidth = 215,
                AccentColor = "#12AB34"
            }
        };

        await store.SaveAsync(expected);
        var actual = await store.LoadAsync();

        Assert.AreEqual(expected.Appearance, actual.Appearance);
        Assert.AreEqual(expected.General, actual.General);
        Assert.AreEqual(expected.Modules["media"].SchemaVersion, actual.Modules["media"].SchemaVersion);
        Assert.AreEqual(expected.Modules["media"].IsEnabled, actual.Modules["media"].IsEnabled);
        Assert.AreEqual(
            expected.Modules["media"].EventDurationSeconds,
            actual.Modules["media"].EventDurationSeconds);
        Assert.AreEqual(0, actual.Modules["media"].Options?.Count);
        Assert.IsTrue(File.Exists(_settingsPath));
    }

    [TestMethod]
    public async Task Load_CorruptJson_ReturnsDefaultsAndQuarantinesFile()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(_settingsPath, "{ definitely not json");
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));

        var result = await store.LoadAsync();

        Assert.AreEqual(MiaDockSettings.Default, result);
        Assert.IsFalse(File.Exists(_settingsPath));
        Assert.AreEqual(1, Directory.GetFiles(_directory, "settings.corrupt-*.json").Length);
    }

    [TestMethod]
    public async Task Load_SchemaThreeWithoutOnboarding_MigratesAsIncomplete()
    {
        Directory.CreateDirectory(_directory);
        var node = JsonNode.Parse(JsonSerializer.Serialize(MiaDockSettings.Default))!.AsObject();
        node["SchemaVersion"] = 3;
        node.Remove("Onboarding");
        await File.WriteAllTextAsync(_settingsPath, node.ToJsonString());
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));

        var result = await store.LoadAsync();

        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.IsFalse(result.Onboarding.IsCompleted);
        Assert.IsTrue(result.Modules.ContainsKey("media"));
    }

    [TestMethod]
    public async Task Load_SchemaFourWithoutModules_AddsMediaEnvelope()
    {
        Directory.CreateDirectory(_directory);
        var node = JsonNode.Parse(JsonSerializer.Serialize(MiaDockSettings.Default))!.AsObject();
        node["SchemaVersion"] = 4;
        node.Remove("Modules");
        await File.WriteAllTextAsync(_settingsPath, node.ToJsonString());
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));

        var result = await store.LoadAsync();

        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.IsTrue(result.Modules["media"].IsEnabled);
        Assert.AreEqual(5, result.Modules["media"].EventDurationSeconds);
        Assert.IsTrue(result.Modules["system-activity"].IsEnabled);
        Assert.IsTrue(result.Modules["battery"].IsEnabled);
        Assert.IsTrue(result.Modules["network"].IsEnabled);
        Assert.IsFalse(result.Modules["bluetooth"].ShowInFullscreen);
    }

    [TestMethod]
    public async Task Load_SchemaTwelveWithoutStoreUpdates_AddsEnabledDefaults()
    {
        Directory.CreateDirectory(_directory);
        var node = JsonNode.Parse(JsonSerializer.Serialize(MiaDockSettings.Default))!.AsObject();
        node["SchemaVersion"] = 12;
        node.Remove("StoreUpdates");
        await File.WriteAllTextAsync(_settingsPath, node.ToJsonString());
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));

        var result = await store.LoadAsync();

        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.AreEqual(StoreUpdateSettings.Default, result.StoreUpdates);
    }

    [TestMethod]
    public async Task Load_SchemaThirteenWithoutClockSettings_AddsDisplayDefaults()
    {
        Directory.CreateDirectory(_directory);
        var node = JsonNode.Parse(JsonSerializer.Serialize(MiaDockSettings.Default))!.AsObject();
        node["SchemaVersion"] = 13;
        node["General"]!.AsObject().Remove("Clock");
        await File.WriteAllTextAsync(_settingsPath, node.ToJsonString());
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));

        var result = await store.LoadAsync();

        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.AreEqual(ClockDisplaySettings.Default, result.General.Clock);
    }

    [TestMethod]
    public async Task Load_SchemaTwentySevenWithoutAudibleNotifications_AddsEnabledDefaults()
    {
        Directory.CreateDirectory(_directory);
        var node = JsonNode.Parse(JsonSerializer.Serialize(MiaDockSettings.Default))!.AsObject();
        node["SchemaVersion"] = 27;
        node.Remove("AudibleNotifications");
        await File.WriteAllTextAsync(_settingsPath, node.ToJsonString());
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));

        var result = await store.LoadAsync();

        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.AreEqual(AudibleNotificationSettings.Default, result.AudibleNotifications);
    }

    [TestMethod]
    public async Task Load_SchemaTwentyEight_AddsHourlyDefaultsWithoutEnablingModule()
    {
        Directory.CreateDirectory(_directory);
        var node = JsonNode.Parse(JsonSerializer.Serialize(MiaDockSettings.Default))!.AsObject();
        node["SchemaVersion"] = 28;
        node["AudibleNotifications"]!.AsObject().Remove("HourlyEnabled");
        node["Modules"]!.AsObject().Remove("hourly-notification");
        await File.WriteAllTextAsync(_settingsPath, node.ToJsonString());
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));

        var result = await store.LoadAsync();

        Assert.AreEqual(29, result.SchemaVersion);
        Assert.IsTrue(result.AudibleNotifications.HourlyEnabled);
        Assert.IsFalse(result.Modules["hourly-notification"].IsEnabled);
    }

    [TestMethod]
    public async Task SaveAndLoad_RoundTripsHourlyPreferences()
    {
        Directory.CreateDirectory(_directory);
        var modules = new Dictionary<string, ModuleSettingsEnvelope>(
            MiaDockSettings.Default.Modules,
            StringComparer.Ordinal)
        {
            ["hourly-notification"] = ModuleSettingsEnvelope.HourlyNotificationDefault with
            {
                IsEnabled = true
            }
        };
        var expected = MiaDockSettings.Default with
        {
            AudibleNotifications = MiaDockSettings.Default.AudibleNotifications with
            {
                HourlyEnabled = false
            },
            Modules = modules
        };
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));

        await store.SaveAsync(expected);
        var result = await store.LoadAsync();

        Assert.IsFalse(result.AudibleNotifications.HourlyEnabled);
        Assert.IsTrue(result.Modules["hourly-notification"].IsEnabled);
    }

    [TestMethod]
    public async Task Load_SchemaFourteenWithoutFocus_AddsBuiltInProfilesWithoutDataLoss()
    {
        Directory.CreateDirectory(_directory);
        var node = JsonNode.Parse(JsonSerializer.Serialize(MiaDockSettings.Default))!.AsObject();
        node["SchemaVersion"] = 14;
        node.Remove("Focus");
        node["Appearance"]!["AccentColor"] = "#123456";
        node["General"]!["Language"] = (int)AppLanguage.English;
        await File.WriteAllTextAsync(_settingsPath, node.ToJsonString());
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));

        var result = await store.LoadAsync();

        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.AreEqual("#123456", result.Appearance.AccentColor);
        Assert.AreEqual(AppLanguage.English, result.General.Language);
        Assert.AreEqual(FocusSettings.Default, result.Focus);
    }

    [TestMethod]
    public async Task SaveAndLoad_RoundTripsCustomFocusProfileAndActiveState()
    {
        var now = DateTimeOffset.UtcNow;
        var custom = new FocusProfile(
            "reading",
            FocusProfileKind.Custom,
            "Reading",
            "book",
            "#22C55E",
            45,
            FocusProfileBehavior.Default,
            Array.Empty<FocusSchedule>(),
            Array.Empty<FocusActivationRule>());
        var expected = MiaDockSettings.Default with
        {
            Focus = new FocusSettings(
                FocusSettings.CurrentSchemaVersion,
                [.. FocusSettings.Default.Profiles, custom],
                new FocusActivationState(
                    "reading",
                    FocusActivationSource.Manual,
                    now,
                    now.AddMinutes(45)))
        };
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));

        await store.SaveAsync(expected);
        var result = await store.LoadAsync();

        var actualCustom = result.Focus.Profiles.Single(profile => profile.Id == "reading");
        Assert.AreEqual("Reading", actualCustom.CustomName);
        Assert.AreEqual("book", actualCustom.IconKey);
        Assert.AreEqual(45, actualCustom.DefaultDurationMinutes);
        Assert.AreEqual("reading", result.Focus.ActiveState?.ProfileId);
        Assert.AreEqual(TimeSpan.Zero, result.Focus.ActiveState?.StartedAtUtc.Offset);
    }

    [TestMethod]
    public async Task Load_SchemaFifteen_PreservesFocusSchedulesAndAutomationRules()
    {
        Directory.CreateDirectory(_directory);
        var profile = FocusProfileDefaults.FindBuiltIn(FocusProfileDefaults.WorkId)! with
        {
            Schedules =
            [
                new FocusSchedule(
                    "weekday",
                    true,
                    FocusDays.Weekdays,
                    9 * 60,
                    17 * 60)
            ],
            ActivationRules =
            [
                new FocusActivationRule(
                    "code",
                    true,
                    FocusActivationRuleKind.ApplicationForeground,
                    "Code")
            ]
        };
        var settings = MiaDockSettings.Default with
        {
            SchemaVersion = 15,
            Focus = new FocusSettings(
                1,
                MiaDockSettings.Default.Focus.Profiles
                    .Select(item => item.Id == profile.Id ? profile : item)
                    .ToArray(),
                null)
        };
        await File.WriteAllTextAsync(
            _settingsPath,
            JsonSerializer.Serialize(settings));
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));

        var result = await store.LoadAsync();

        Assert.AreEqual(MiaDockSettings.CurrentSchemaVersion, result.SchemaVersion);
        Assert.AreEqual(FocusSettings.CurrentSchemaVersion, result.Focus.SchemaVersion);
        var migrated = result.Focus.Profiles.Single(item => item.Id == profile.Id);
        Assert.HasCount(1, migrated.Schedules);
        Assert.HasCount(1, migrated.ActivationRules);
        Assert.AreEqual("code.exe", migrated.ActivationRules[0].Target);
    }

    [TestMethod]
    public async Task Load_FocusSchemaTwo_KeepsDoNotDisturbDockReachable()
    {
        Directory.CreateDirectory(_directory);
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
                MiaDockSettings.Default.Focus.Profiles
                    .Select(profile => profile.Id == legacy.Id ? legacy : profile)
                    .ToArray(),
                active)
        };
        await File.WriteAllTextAsync(
            _settingsPath,
            JsonSerializer.Serialize(settings));
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));

        var result = await store.LoadAsync();
        var migrated = result.Focus.Profiles.Single(profile =>
            profile.Id == FocusProfileDefaults.DoNotDisturbId);

        Assert.AreEqual(
            FocusSettings.CurrentSchemaVersion,
            result.Focus.SchemaVersion);
        Assert.AreEqual(
            FocusDockVisibility.UseGlobalSetting,
            migrated.Behavior.DockVisibility);
        Assert.AreEqual(active, result.Focus.ActiveState);
    }

    [TestMethod]
    public async Task Load_OneCorruptModule_RepairsOnlyThatModuleAndRewritesValidJson()
    {
        Directory.CreateDirectory(_directory);
        var expected = MiaDockSettings.Default with
        {
            Appearance = MiaDockSettings.Default.Appearance with { AccentColor = "#123456" },
            General = MiaDockSettings.Default.General with { Language = AppLanguage.English }
        };
        var node = JsonNode.Parse(JsonSerializer.Serialize(expected))!.AsObject();
        var modules = node["Modules"]!.AsObject();
        modules["media"]!["EventDurationSeconds"] = 17;
        modules["timer"]!["EventDurationSeconds"] = "not-a-number";
        await File.WriteAllTextAsync(_settingsPath, node.ToJsonString());
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));

        var result = await store.LoadAsync();
        var secondLoad = await store.LoadAsync();

        Assert.AreEqual("#123456", result.Appearance.AccentColor);
        Assert.AreEqual(AppLanguage.English, result.General.Language);
        Assert.AreEqual(17, result.Modules["media"].EventDurationSeconds);
        Assert.AreEqual(ModuleSettingsEnvelope.TimerDefault, result.Modules["timer"]);
        Assert.AreEqual(result.Appearance, secondLoad.Appearance);
        Assert.AreEqual(result.General, secondLoad.General);
        Assert.AreEqual(result.Modules["media"].EventDurationSeconds, secondLoad.Modules["media"].EventDurationSeconds);
        Assert.AreEqual(result.Modules["timer"].EventDurationSeconds, secondLoad.Modules["timer"].EventDurationSeconds);
        Assert.HasCount(0, Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [TestMethod]
    public async Task Load_OneCorruptSection_PreservesOtherSections()
    {
        Directory.CreateDirectory(_directory);
        var node = JsonNode.Parse(JsonSerializer.Serialize(MiaDockSettings.Default))!.AsObject();
        node["General"] = "invalid-general-section";
        node["Appearance"]!["CollapsedWidth"] = 244;
        node["Privacy"]!["ShowSensitiveContentInFullscreen"] = true;
        await File.WriteAllTextAsync(_settingsPath, node.ToJsonString());
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));

        var result = await store.LoadAsync();

        Assert.AreEqual(GeneralSettings.Default, result.General);
        Assert.AreEqual(244, result.Appearance.CollapsedWidth);
        Assert.IsTrue(result.Privacy.ShowSensitiveContentInFullscreen);
        Assert.AreEqual(
            MiaDockSettings.Default.Modules["media"].EventDurationSeconds,
            result.Modules["media"].EventDurationSeconds);
        Assert.AreEqual(
            MiaDockSettings.Default.Modules["media"].IsEnabled,
            result.Modules["media"].IsEnabled);
    }

    [TestMethod]
    public async Task Load_CorruptAppearanceSection_ResetsOnlyAppearance()
    {
        Directory.CreateDirectory(_directory);
        var node = JsonNode.Parse(JsonSerializer.Serialize(MiaDockSettings.Default))!.AsObject();
        node["Appearance"]!["Motion"]!["Speed"] = "invalid-speed";
        node["General"]!["Language"] = (int)AppLanguage.English;
        node["Privacy"]!["ShowSensitiveContentInFullscreen"] = true;
        await File.WriteAllTextAsync(_settingsPath, node.ToJsonString());
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));

        var result = await store.LoadAsync();
        var persisted = await store.LoadAsync();

        Assert.AreEqual(AppearanceSettings.Default, result.Appearance);
        Assert.AreEqual(AppLanguage.English, result.General.Language);
        Assert.IsTrue(result.Privacy.ShowSensitiveContentInFullscreen);
        Assert.AreEqual(result.Appearance, persisted.Appearance);
        Assert.AreEqual(result.General, persisted.General);
        Assert.HasCount(0, Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [TestMethod]
    public async Task Load_CorruptFocusSection_ResetsOnlyFocusAndRewritesValidJson()
    {
        Directory.CreateDirectory(_directory);
        var node = JsonNode.Parse(JsonSerializer.Serialize(MiaDockSettings.Default))!.AsObject();
        node["Focus"] = "invalid-focus-section";
        node["Appearance"]!["AccentColor"] = "#654321";
        node["General"]!["Language"] = (int)AppLanguage.English;
        await File.WriteAllTextAsync(_settingsPath, node.ToJsonString());
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));

        var result = await store.LoadAsync();
        var secondLoad = await store.LoadAsync();

        Assert.AreEqual(FocusSettings.Default, result.Focus);
        Assert.AreEqual("#654321", result.Appearance.AccentColor);
        Assert.AreEqual(AppLanguage.English, result.General.Language);
        CollectionAssert.AreEqual(
            result.Focus.Profiles.Select(profile => profile.Id).ToArray(),
            secondLoad.Focus.Profiles.Select(profile => profile.Id).ToArray());
        Assert.AreEqual(result.Focus.ActiveState, secondLoad.Focus.ActiveState);
        Assert.AreEqual(result.Appearance, secondLoad.Appearance);
        Assert.HasCount(0, Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    [TestMethod]
    public async Task Load_FileTemporarilyLocked_ReturnsDefaultsWithoutCrashing()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(_settingsPath, JsonSerializer.Serialize(MiaDockSettings.Default));
        await using var lockStream = new FileStream(
            _settingsPath,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.None);
        var store = new JsonSettingsStore(new FixedPathProvider(_settingsPath));

        var result = await store.LoadAsync();

        Assert.AreEqual(MiaDockSettings.Default, result);
        Assert.IsTrue(File.Exists(_settingsPath));
        Assert.HasCount(0, Directory.GetFiles(_directory, "settings.corrupt-*.json"));
    }

    private sealed class FixedPathProvider(string path) : ISettingsPathProvider
    {
        public string GetSettingsFilePath() => path;
    }
}
