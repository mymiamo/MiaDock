using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using NAudio.CoreAudioApi;

namespace MiaDock.Platform.Windows.Audio;

/// <summary>
/// Read-only endpoint catalog backed by NAudio's Core Audio projection. NAudio
/// owns COM activation and endpoint-property lifetime here; custom Core Audio
/// interop remains only for session-level mixer and selected-media metering.
/// </summary>
public sealed class WindowsAudioDeviceCatalog : IAudioDeviceCatalog
{
    public Task<IReadOnlyList<AudioDeviceInfo>> GetOutputDevicesAsync(CancellationToken cancellationToken = default) =>
        EnumerateAsync(DataFlow.Render, cancellationToken);

    public Task<IReadOnlyList<AudioDeviceInfo>> GetInputDevicesAsync(CancellationToken cancellationToken = default) =>
        EnumerateAsync(DataFlow.Capture, cancellationToken);

    private static Task<IReadOnlyList<AudioDeviceInfo>> EnumerateAsync(
        DataFlow flow,
        CancellationToken cancellationToken) =>
        Task.Run(() => Enumerate(flow, cancellationToken), cancellationToken);

    private static IReadOnlyList<AudioDeviceInfo> Enumerate(DataFlow flow, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var enumerator = new MMDeviceEnumerator();
        using var defaultConsole = TryGetDefaultDevice(enumerator, flow, Role.Console);
        using var defaultCommunications = TryGetDefaultDevice(enumerator, flow, Role.Communications);
        using var devices = enumerator.EnumerateAudioEndPoints(flow, DeviceState.Active);

        var defaultConsoleId = defaultConsole?.ID;
        var defaultCommunicationsId = defaultCommunications?.ID;
        var result = new List<AudioDeviceInfo>(devices.Count);
        foreach (var device in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (device)
            {
                var id = device.ID;
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var name = device.FriendlyName;
                result.Add(new AudioDeviceInfo(
                    id,
                    string.IsNullOrWhiteSpace(name) ? id : name.Trim(),
                    string.Equals(id, defaultConsoleId, StringComparison.Ordinal),
                    string.Equals(id, defaultCommunicationsId, StringComparison.Ordinal),
                    true));
            }
        }

        return result;
    }

    private static MMDevice? TryGetDefaultDevice(MMDeviceEnumerator enumerator, DataFlow flow, Role role)
    {
        try
        {
            return enumerator.GetDefaultAudioEndpoint(flow, role);
        }
        catch
        {
            // A machine can temporarily have no endpoint for one role. The
            // catalog still returns its active endpoints without default flags.
            return null;
        }
    }
}
