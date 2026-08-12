namespace MiaDock.Modules.SystemStatus.Models;

public sealed record PrivacyApplication(
    string Id,
    int? ProcessId,
    string ProcessName,
    string DisplayName,
    string? ExecutablePath,
    bool UsesMicrophone,
    bool UsesCamera)
{
    public bool UsesBoth => UsesMicrophone && UsesCamera;
}
