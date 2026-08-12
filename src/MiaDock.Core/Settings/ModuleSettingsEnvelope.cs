using System.Text.Json;

namespace MiaDock.Core.Settings;

public sealed record ModuleSettingsEnvelope(
    int SchemaVersion,
    bool IsEnabled,
    double EventDurationSeconds,
    bool ShowInFullscreen,
    IReadOnlyDictionary<string, JsonElement>? Options)
{
    public const int InitialSchemaVersion = 1;

    public static ModuleSettingsEnvelope MediaDefault { get; } = new(
        InitialSchemaVersion,
        true,
        5,
        true,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal));

    public static ModuleSettingsEnvelope SystemActivityDefault { get; } = new(
        InitialSchemaVersion,
        true,
        3,
        true,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal));

    public static ModuleSettingsEnvelope PrivacyDefault { get; } = new(
        InitialSchemaVersion,
        true,
        3.5,
        true,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal));

    public static ModuleSettingsEnvelope VolumeDefault { get; } = new(
        InitialSchemaVersion,
        true,
        2.5,
        true,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["showOutputDeviceName"] = JsonSerializer.SerializeToElement(true)
        });

    public static ModuleSettingsEnvelope BatteryDefault { get; } = new(
        InitialSchemaVersion,
        true,
        5,
        true,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["lowThresholdPercent"] = JsonSerializer.SerializeToElement(20),
            ["criticalThresholdPercent"] = JsonSerializer.SerializeToElement(10),
            ["emergencyThresholdPercent"] = JsonSerializer.SerializeToElement(5)
        });

    public static ModuleSettingsEnvelope NetworkDefault { get; } = new(
        InitialSchemaVersion,
        true,
        3,
        true,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal));

    public static ModuleSettingsEnvelope BluetoothDefault { get; } = new(
        InitialSchemaVersion,
        true,
        3,
        false,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal));

    public static ModuleSettingsEnvelope TimerDefault { get; } = new(
        InitialSchemaVersion,
        true,
        5,
        true,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["presetMinutes"] = JsonSerializer.SerializeToElement(new[] { 5, 10, 15, 25, 30, 45, 60 })
        });

    public static ModuleSettingsEnvelope NotificationsDefault { get; } = new(
        InitialSchemaVersion,
        false,
        5,
        false,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["useAllowList"] = JsonSerializer.SerializeToElement(false),
            ["allowedApplications"] = JsonSerializer.SerializeToElement(Array.Empty<string>()),
            ["blockedApplications"] = JsonSerializer.SerializeToElement(Array.Empty<string>()),
            ["bodyAllowedApplications"] = JsonSerializer.SerializeToElement(Array.Empty<string>())
        });

    public static ModuleSettingsEnvelope TransfersDefault { get; } = new(
        InitialSchemaVersion,
        true,
        5,
        false,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal));
}
