using System.Text.Json;
using MiaDock.Core.Settings;

namespace MiaDock.Modules.SystemStatus.Settings;

public sealed record VolumeModuleOptions(
    TimeSpan EventDuration,
    bool ShowInFullscreen,
    bool ShowOutputDeviceName)
{
    public static VolumeModuleOptions Default { get; } = new(
        TimeSpan.FromSeconds(2.5),
        true,
        true);

    public static VolumeModuleOptions FromEnvelope(ModuleSettingsEnvelope? envelope) => new(
        TimeSpan.FromSeconds(Math.Clamp(
            envelope?.EventDurationSeconds ?? Default.EventDuration.TotalSeconds,
            1,
            10)),
        envelope?.ShowInFullscreen ?? Default.ShowInFullscreen,
        ReadBool(
            envelope?.Options,
            "showOutputDeviceName",
            Default.ShowOutputDeviceName));

    public static ModuleSettingsEnvelope ToEnvelope(
        VolumeModuleOptions options,
        bool isEnabled = true) => new(
        ModuleSettingsEnvelope.InitialSchemaVersion,
        isEnabled,
        Math.Clamp(options.EventDuration.TotalSeconds, 1, 10),
        options.ShowInFullscreen,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["showOutputDeviceName"] = JsonSerializer.SerializeToElement(
                options.ShowOutputDeviceName)
        });

    private static bool ReadBool(
        IReadOnlyDictionary<string, JsonElement>? options,
        string key,
        bool fallback) =>
        options is not null &&
        options.TryGetValue(key, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;
}
