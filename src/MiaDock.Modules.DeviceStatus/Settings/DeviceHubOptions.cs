using System.Text.Json;
using MiaDock.Core.Settings;

namespace MiaDock.Modules.DeviceStatus.Settings;

public sealed record DeviceHubOptions(
    bool IsEnabled,
    bool ShowConnectedEvents,
    bool ShowDisconnectedEvents,
    bool ShowStorageEvents,
    bool ShowBatteryWarnings,
    bool ShowAudioOutputEvents,
    bool ShowBluetooth,
    bool ShowAudioDevices,
    bool ShowRemovableStorage,
    int BatteryWarningPercent,
    TimeSpan EventDuration,
    bool ShowInFullscreen)
{
    public static DeviceHubOptions Default { get; } = new(true, true, true, true, true, true, true, true, true, 20, TimeSpan.FromSeconds(4), false);

    public static DeviceHubOptions FromEnvelope(ModuleSettingsEnvelope? envelope)
    {
        var options = envelope?.Options;
        return new DeviceHubOptions(
            envelope?.IsEnabled ?? Default.IsEnabled,
            ReadBool(options, "showConnectedEvents", Default.ShowConnectedEvents),
            ReadBool(options, "showDisconnectedEvents", Default.ShowDisconnectedEvents),
            ReadBool(options, "showStorageEvents", Default.ShowStorageEvents),
            ReadBool(options, "showBatteryWarnings", Default.ShowBatteryWarnings),
            ReadBool(options, "showAudioOutputEvents", Default.ShowAudioOutputEvents),
            ReadBool(options, "showBluetooth", Default.ShowBluetooth),
            ReadBool(options, "showAudioDevices", Default.ShowAudioDevices),
            ReadBool(options, "showRemovableStorage", Default.ShowRemovableStorage),
            Math.Clamp(ReadInt(options, "batteryWarningPercent", Default.BatteryWarningPercent), 5, 50),
            TimeSpan.FromSeconds(Math.Clamp(envelope?.EventDurationSeconds ?? 4, 1, 60)),
            envelope?.ShowInFullscreen ?? Default.ShowInFullscreen);
    }

    private static bool ReadBool(IReadOnlyDictionary<string, JsonElement>? options, string key, bool fallback) =>
        options?.TryGetValue(key, out var value) == true && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean() : fallback;

    private static int ReadInt(IReadOnlyDictionary<string, JsonElement>? options, string key, int fallback) =>
        options?.TryGetValue(key, out var value) == true && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result : fallback;
}
