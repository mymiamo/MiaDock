using System.Text.Json;
using MiaDock.Core.Settings;

namespace MiaDock.Modules.DeviceStatus.Settings;

public sealed record BatteryModuleOptions(
    int LowThresholdPercent,
    int CriticalThresholdPercent,
    int EmergencyThresholdPercent,
    TimeSpan EventDuration,
    bool ShowInFullscreen)
{
    public static BatteryModuleOptions Default { get; } = new(
        20,
        10,
        5,
        TimeSpan.FromSeconds(5),
        true);

    public static BatteryModuleOptions FromEnvelope(ModuleSettingsEnvelope? envelope)
    {
        var options = envelope?.Options;
        var low = ReadInt(options, "lowThresholdPercent", Default.LowThresholdPercent);
        var critical = ReadInt(options, "criticalThresholdPercent", Default.CriticalThresholdPercent);
        var emergency = ReadInt(options, "emergencyThresholdPercent", Default.EmergencyThresholdPercent);

        emergency = Math.Clamp(emergency, 1, 20);
        critical = Math.Clamp(critical, emergency + 1, 35);
        low = Math.Clamp(low, critical + 1, 50);

        return new BatteryModuleOptions(
            low,
            critical,
            emergency,
            TimeSpan.FromSeconds(Math.Clamp(
                envelope?.EventDurationSeconds ?? Default.EventDuration.TotalSeconds,
                1,
                60)),
            envelope?.ShowInFullscreen ?? Default.ShowInFullscreen);
    }

    public static ModuleSettingsEnvelope ApplyThresholds(
        ModuleSettingsEnvelope envelope,
        int low,
        int critical,
        int emergency)
    {
        var normalized = FromEnvelope(envelope with
        {
            Options = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["lowThresholdPercent"] = JsonSerializer.SerializeToElement(low),
                ["criticalThresholdPercent"] = JsonSerializer.SerializeToElement(critical),
                ["emergencyThresholdPercent"] = JsonSerializer.SerializeToElement(emergency)
            }
        });

        return envelope with
        {
            Options = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["lowThresholdPercent"] = JsonSerializer.SerializeToElement(normalized.LowThresholdPercent),
                ["criticalThresholdPercent"] = JsonSerializer.SerializeToElement(normalized.CriticalThresholdPercent),
                ["emergencyThresholdPercent"] = JsonSerializer.SerializeToElement(normalized.EmergencyThresholdPercent)
            }
        };
    }

    private static int ReadInt(
        IReadOnlyDictionary<string, JsonElement>? options,
        string key,
        int fallback) =>
        options is not null &&
        options.TryGetValue(key, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var result)
            ? result
            : fallback;
}
