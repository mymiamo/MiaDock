using MiaDock.Core.Focus;

namespace MiaDock.Core.Settings;

public sealed record FocusSettings(
    int SchemaVersion,
    IReadOnlyList<FocusProfile> Profiles,
    FocusActivationState? ActiveState,
    bool IsEnabled = true)
{
    public const int CurrentSchemaVersion = 4;

    public static FocusSettings Default { get; } = new(
        CurrentSchemaVersion,
        FocusProfileDefaults.All,
        null,
        true);
}
