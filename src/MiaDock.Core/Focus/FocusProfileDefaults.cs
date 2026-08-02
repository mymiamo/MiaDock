using MiaDock.Core.Modules;

namespace MiaDock.Core.Focus;

public static class FocusProfileDefaults
{
    public const string WorkId = "work";
    public const string GamingId = "gaming";
    public const string SleepId = "sleep";
    public const string DoNotDisturbId = "do-not-disturb";

    public static IReadOnlySet<string> BuiltInIds { get; } = new HashSet<string>(
        [WorkId, GamingId, SleepId, DoNotDisturbId],
        StringComparer.Ordinal);

    public static IReadOnlySet<string> AllowedIconKeys { get; } = new HashSet<string>(
        ["briefcase", "game-controller", "moon", "do-not-disturb", "star", "book", "fitness", "leaf"],
        StringComparer.Ordinal);

    public static IReadOnlyList<FocusProfile> All { get; } =
    [
        CreateBuiltIn(
            WorkId,
            FocusProfileKind.Work,
            "briefcase",
            "#3B82F6",
            new FocusProfileBehavior(
                FocusDockVisibility.UseGlobalSetting,
                Array.Empty<string>(),
                ModuleEventPriority.Normal,
                true,
                true,
                true)),
        CreateBuiltIn(
            GamingId,
            FocusProfileKind.Gaming,
            "game-controller",
            "#8B5CF6",
            new FocusProfileBehavior(
                FocusDockVisibility.EventsOnly,
                ["battery", "timer", "media", "volume", "system-activity"],
                ModuleEventPriority.High,
                true,
                false,
                false)),
        CreateBuiltIn(
            SleepId,
            FocusProfileKind.Sleep,
            "moon",
            "#6366F1",
            new FocusProfileBehavior(
                FocusDockVisibility.EventsOnly,
                ["battery", "timer"],
                ModuleEventPriority.Critical,
                false,
                false,
                false)),
        CreateBuiltIn(
            DoNotDisturbId,
            FocusProfileKind.DoNotDisturb,
            "do-not-disturb",
            "#EF4444",
            new FocusProfileBehavior(
                FocusDockVisibility.UseGlobalSetting,
                ["battery", "timer"],
                ModuleEventPriority.High,
                false,
                false,
                false))
    ];

    public static FocusProfile ForKind(FocusProfileKind kind) => kind switch
    {
        FocusProfileKind.Work => All[0],
        FocusProfileKind.Gaming => All[1],
        FocusProfileKind.Sleep => All[2],
        FocusProfileKind.DoNotDisturb => All[3],
        _ => All[0]
    };

    public static FocusProfile? FindBuiltIn(string id) =>
        All.FirstOrDefault(profile => profile.Id.Equals(id, StringComparison.Ordinal));

    public static string GetDisplayNameKey(FocusProfile profile) =>
        profile.Kind == FocusProfileKind.Custom
            ? string.Empty
            : $"Focus.Profile.{profile.Kind}.Name";

    private static FocusProfile CreateBuiltIn(
        string id,
        FocusProfileKind kind,
        string iconKey,
        string color,
        FocusProfileBehavior behavior) =>
        new(
            id,
            kind,
            null,
            iconKey,
            color,
            null,
            behavior,
            Array.Empty<FocusSchedule>(),
            Array.Empty<FocusActivationRule>());
}
