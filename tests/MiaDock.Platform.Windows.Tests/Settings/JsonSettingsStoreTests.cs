using MiaDock.Core.Settings;
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
