namespace MiaDock.Modules.SystemStatus.Models;

public sealed record SystemActivitySnapshot(
    SystemActivityServiceState ServiceState,
    bool IsMasterVolumeAvailable,
    double MasterVolume,
    bool IsMasterMuted,
    ApplicationVolumeAvailability ApplicationVolumeAvailability,
    double ApplicationVolume,
    bool IsApplicationMuted,
    MicrophoneUsageState MicrophoneUsage,
    CameraDeviceAvailability CameraDeviceAvailability,
    CameraAccessState CameraAccess,
    CallActivityState CallActivity)
{
    public static SystemActivitySnapshot Default { get; } = new(
        SystemActivityServiceState.NotInitialized,
        false,
        0,
        false,
        ApplicationVolumeAvailability.Unavailable,
        0,
        false,
        MicrophoneUsageState.Unavailable,
        CameraDeviceAvailability.Unavailable,
        CameraAccessState.Unavailable,
        CallActivityState.None);

    public int MasterVolumePercent => (int)Math.Round(Math.Clamp(MasterVolume, 0, 1) * 100);

    public int ApplicationVolumePercent => (int)Math.Round(Math.Clamp(ApplicationVolume, 0, 1) * 100);
}
