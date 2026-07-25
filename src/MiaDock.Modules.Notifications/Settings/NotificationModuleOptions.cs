using System.Text.Json;
using MiaDock.Core.Settings;

namespace MiaDock.Modules.Notifications.Settings;

public sealed record NotificationModuleOptions(
    bool IsEnabled,
    TimeSpan EventDuration,
    bool ShowInFullscreen,
    bool UseAllowList,
    IReadOnlySet<string> AllowedApplications,
    IReadOnlySet<string> BlockedApplications,
    IReadOnlySet<string> BodyAllowedApplications)
{
    public static NotificationModuleOptions Default { get; } = new(
        false,
        TimeSpan.FromSeconds(5),
        false,
        false,
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal));

    public bool IsApplicationAllowed(string sourceId) =>
        !BlockedApplications.Contains(sourceId) &&
        (!UseAllowList || AllowedApplications.Contains(sourceId));

    public bool CanShowBody(string sourceId) => BodyAllowedApplications.Contains(sourceId);

    public static NotificationModuleOptions FromEnvelope(ModuleSettingsEnvelope? envelope) => new(
        envelope?.IsEnabled ?? Default.IsEnabled,
        TimeSpan.FromSeconds(Math.Clamp(envelope?.EventDurationSeconds ?? 5, 1, 60)),
        envelope?.ShowInFullscreen ?? false,
        ReadBool(envelope?.Options, "useAllowList"),
        ReadSet(envelope?.Options, "allowedApplications"),
        ReadSet(envelope?.Options, "blockedApplications"),
        ReadSet(envelope?.Options, "bodyAllowedApplications"));

    public static ModuleSettingsEnvelope ToEnvelope(NotificationModuleOptions options) => new(
        1,
        options.IsEnabled,
        Math.Clamp(options.EventDuration.TotalSeconds, 1, 60),
        options.ShowInFullscreen,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["useAllowList"] = JsonSerializer.SerializeToElement(options.UseAllowList),
            ["allowedApplications"] = JsonSerializer.SerializeToElement(options.AllowedApplications.Order(StringComparer.Ordinal)),
            ["blockedApplications"] = JsonSerializer.SerializeToElement(options.BlockedApplications.Order(StringComparer.Ordinal)),
            ["bodyAllowedApplications"] = JsonSerializer.SerializeToElement(options.BodyAllowedApplications.Order(StringComparer.Ordinal))
        });

    private static bool ReadBool(IReadOnlyDictionary<string, JsonElement>? options, string key) =>
        options is not null && options.TryGetValue(key, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();

    private static IReadOnlySet<string> ReadSet(
        IReadOnlyDictionary<string, JsonElement>? options,
        string key)
    {
        if (options is null || !options.TryGetValue(key, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
    }
}
