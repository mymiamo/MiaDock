namespace MiaDock.Core.Settings;

public sealed record MonitorAppearanceProfile(string Id, string Name, string? DisplayId,
    IslandPositionSetting Position, AppearanceSettings Appearance)
{
    public static MiaDockSettings Resolve(MiaDockSettings settings, string displayId)
    {
        var profile = settings.DisplayProfiles?.FirstOrDefault(item => item.DisplayId == displayId);
        return profile is null ? settings : settings with
        {
            Appearance = profile.Appearance,
            General = settings.General with { Position = profile.Position }
        };
    }

    public static IReadOnlyList<MonitorAppearanceProfile> Normalize(IReadOnlyList<MonitorAppearanceProfile>? profiles)
    {
        var normalized = new List<MonitorAppearanceProfile>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var displays = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in (profiles ?? []).Take(32))
        {
            if (profile is null || string.IsNullOrWhiteSpace(profile.Id) || profile.Id.Length > 64 ||
                string.IsNullOrWhiteSpace(profile.Name) || profile.Appearance is null || !ids.Add(profile.Id)) continue;
            var displayId = string.IsNullOrWhiteSpace(profile.DisplayId) || profile.DisplayId.Length > 256 ? null : profile.DisplayId;
            if (displayId is not null && !displays.Add(displayId)) displayId = null;
            var appearance = SettingsValidator.Normalize(MiaDockSettings.Default with { Appearance = profile.Appearance }).Appearance;
            normalized.Add(profile with { Name = profile.Name.Trim()[..Math.Min(profile.Name.Trim().Length, 64)],
                DisplayId = displayId, Appearance = appearance,
                Position = Enum.IsDefined(profile.Position) ? profile.Position : IslandPositionSetting.TopCenter });
            if (normalized.Count == 16) break;
        }
        return profiles is not null && profiles.SequenceEqual(normalized) ? profiles
            : normalized.Count == 0 ? Array.Empty<MonitorAppearanceProfile>() : normalized;
    }
}
