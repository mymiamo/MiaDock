namespace MiaDock.Core.Modules;

public sealed record ModuleCommandDescriptor
{
    public ModuleCommandDescriptor(string id, string displayName, string glyph)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(glyph);
        Id = id;
        DisplayName = displayName;
        Glyph = glyph;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string Glyph { get; }
}
