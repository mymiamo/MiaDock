using MiaDock.Core.Modules;

namespace MiaDock.Core.Settings;

public sealed record DockNavigationSettings(IReadOnlyList<string> PinnedModuleIds)
{
    public static DockNavigationSettings Default { get; } = new(["media", "volume", "timer"]);

    public static DockNavigationSettings Normalize(DockNavigationSettings? settings)
    {
        var pins = (settings?.PinnedModuleIds ?? Default.PinnedModuleIds)
        .Where(id => !string.IsNullOrWhiteSpace(id) && id.Length <= 64)
        .Select(id => id.Trim()).Distinct(StringComparer.Ordinal).Take(32).ToArray();
        return settings?.PinnedModuleIds is { } original && original.SequenceEqual(pins, StringComparer.Ordinal)
            ? settings : new(pins);
    }

    public DockNavigationSettings Toggle(string id)
    {
        var ids = Normalize(this).PinnedModuleIds.ToList();
        if (!ids.Remove(id)) ids.Add(id);
        return Normalize(new(ids));
    }

    public DockNavigationSettings Move(string id, int direction)
    {
        var ids = Normalize(this).PinnedModuleIds.ToList();
        var from = ids.IndexOf(id);
        var to = from + Math.Sign(direction);
        if (from >= 0 && to >= 0 && to < ids.Count) (ids[from], ids[to]) = (ids[to], ids[from]);
        return new(ids);
    }

    public static IEnumerable<ModuleDisplayState> Order(IEnumerable<ModuleDisplayState> modules, IReadOnlyList<string> pins)
    {
        var order = pins.Select((id, index) => (id, index)).DistinctBy(item => item.id)
            .ToDictionary(item => item.id, item => item.index, StringComparer.Ordinal);
        return modules.OrderBy(item => order.GetValueOrDefault(item.Descriptor.Id, int.MaxValue))
            .ThenByDescending(item => item.Descriptor.PersistentPriority)
            .ThenBy(item => item.Descriptor.Id, StringComparer.Ordinal);
    }
}
