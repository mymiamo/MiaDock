namespace MiaDock.Core.Settings;

public sealed record MediaSettings(
    string? SelectedSourceId,
    MediaFallbackSetting Fallback,
    VolumeTargetSetting VolumeTarget)
{
    public static MediaSettings Default { get; } = new(
        null,
        MediaFallbackSetting.UseAnotherActiveSession,
        VolumeTargetSetting.SystemMaster);
}
