namespace MiaDock.Core.Modules;

public sealed record ModuleCommandState(
    string Id,
    string DisplayName,
    string Glyph,
    bool IsEnabled);
