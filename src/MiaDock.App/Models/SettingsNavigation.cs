namespace MiaDock.App.Models;

internal sealed record SettingsSubpageDefinition(
    string Id,
    string CategoryId,
    string Title,
    string Description,
    string Glyph,
    string? FocusTarget = null);

internal sealed record SettingsCategoryDefinition(
    string Id,
    string Title,
    string Description,
    string Glyph,
    string ColorResourceKey,
    IReadOnlyList<SettingsSubpageDefinition> Subpages);
