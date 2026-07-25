namespace MiaDock.Core.Settings;

public sealed record PresentationPrivacySettings(
    bool ShowSensitiveContentInFullscreen,
    bool ShowSensitiveContentWhenLocked)
{
    public static PresentationPrivacySettings Default { get; } = new(false, false);
}
