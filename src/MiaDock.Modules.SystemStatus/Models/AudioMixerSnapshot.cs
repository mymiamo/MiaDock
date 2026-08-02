namespace MiaDock.Modules.SystemStatus.Models;

public sealed record AudioMixerSnapshot(
    SystemActivityServiceState ServiceState,
    string? OutputDeviceId,
    string? OutputDeviceName,
    IReadOnlyList<AudioMixerSessionSnapshot> Sessions,
    bool IsMeteringEnabled)
{
    public static AudioMixerSnapshot Default { get; } = new(
        SystemActivityServiceState.NotInitialized,
        null,
        null,
        Array.Empty<AudioMixerSessionSnapshot>(),
        false);
}
