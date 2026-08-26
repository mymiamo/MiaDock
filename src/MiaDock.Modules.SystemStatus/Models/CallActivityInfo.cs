namespace MiaDock.Modules.SystemStatus.Models;

public sealed record CallActivityInfo(
    CallActivityState State,
    CommunicationApp App,
    string? DisplayName,
    string? ProcessName,
    string? ExecutablePath,
    DateTimeOffset? StartedAt,
    bool HasCaptureActivity,
    bool HasRenderActivity)
{
    public static CallActivityInfo None { get; } = new(
        CallActivityState.None,
        CommunicationApp.Unknown,
        null,
        null,
        null,
        null,
        false,
        false);
}
