namespace MiaDock.Modules.SystemStatus.Models;

public sealed record AudioMixerSessionSnapshot(
    string SessionKey,
    uint ProcessId,
    string DisplayName,
    string ProcessName,
    string? IconPath,
    double Volume,
    bool IsMuted,
    bool CanControlVolume,
    bool IsSystemSounds,
    double PeakLevel)
{
    public int VolumePercent =>
        (int)Math.Round(Math.Clamp(Volume, 0, 1) * 100);
}
