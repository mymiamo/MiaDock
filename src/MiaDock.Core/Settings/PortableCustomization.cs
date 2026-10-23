using System.Text;
using System.Text.Json;
using MiaDock.Core.Focus;

namespace MiaDock.Core.Settings;

public sealed record CustomizationPackage(string Format, int Version, AppearanceSettings Appearance,
    IslandPositionSetting Position, ClockDisplaySettings Clock, DockNavigationSettings Navigation,
    IReadOnlyList<FocusProfile> FocusProfiles, IReadOnlyList<MonitorAppearanceProfile> DisplayProfiles);

public static class PortableCustomization
{
    public const int MaximumBytes = 1024 * 1024;
    private const string Format = "miadock-personalization";
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, MaxDepth = 32 };

    public static string Export(MiaDockSettings settings)
    {
        var normalized = SettingsValidator.Normalize(settings);
        var package = new CustomizationPackage(Format, 1, normalized.Appearance, normalized.General.Position,
            normalized.General.Clock, normalized.EffectiveNavigation,
            normalized.Focus.Profiles.Select(profile => profile with { ActivationRules = [] }).ToArray(),
            (normalized.DisplayProfiles ?? []).Select(profile => profile with { DisplayId = null }).ToArray());
        return JsonSerializer.Serialize(package, Options);
    }

    public static CustomizationPackage Read(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new InvalidDataException("The personalization file is too large.");
        var package = JsonSerializer.Deserialize<CustomizationPackage>(json, Options)
            ?? throw new InvalidDataException("Empty personalization file.");
        Validate(package);
        return package;
    }

    public static MiaDockSettings Apply(MiaDockSettings current, CustomizationPackage package)
    {
        Validate(package);
        return SettingsValidator.Normalize(current with
        {
            Appearance = package.Appearance,
            General = current.General with { Position = package.Position, Clock = package.Clock },
            Navigation = package.Navigation,
            Focus = current.Focus with { Profiles = package.FocusProfiles.Select(profile => profile with { ActivationRules = [] }).ToArray(), ActiveState = null },
            DisplayProfiles = package.DisplayProfiles.Select(profile => profile with { DisplayId = null }).ToArray()
        });
    }

    private static void Validate(CustomizationPackage package)
    {
        if (package.Format != Format || package.Version != 1 || package.Appearance is null ||
            package.Clock is null || package.Navigation is null || package.FocusProfiles is null || package.DisplayProfiles is null ||
            package.FocusProfiles.Any(profile => profile is null) || package.DisplayProfiles.Any(profile => profile is null))
            throw new InvalidDataException("Unsupported personalization format or version.");
    }
}
