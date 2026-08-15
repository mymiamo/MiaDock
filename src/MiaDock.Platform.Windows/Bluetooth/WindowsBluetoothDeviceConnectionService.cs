using MiaDock.Core.Logging;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;

namespace MiaDock.Platform.Windows.Bluetooth;

public sealed class WindowsBluetoothDeviceConnectionService : IBluetoothDeviceConnectionService
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(10);
    private readonly IBluetoothRadioStateProvider _radio;
    private readonly IBluetoothProfileController _profiles;
    private readonly IBluetoothAclConnector _acl;
    private readonly ILogService? _log;

    public WindowsBluetoothDeviceConnectionService(
        IBluetoothRadioStateProvider radio,
        ILogService? log = null)
        : this(radio, new Win32BluetoothProfileController(log), new WinRtBluetoothAclConnector(), log)
    {
    }

    internal WindowsBluetoothDeviceConnectionService(
        IBluetoothRadioStateProvider radio,
        IBluetoothProfileController profiles,
        IBluetoothAclConnector acl,
        ILogService? log = null)
    {
        _radio = radio;
        _profiles = profiles;
        _acl = acl;
        _log = log;
    }

    public Task<BluetoothConnectionResult> ConnectAsync(
        BluetoothConnectionRequest request,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(request, enable: true, cancellationToken);

    public Task<BluetoothConnectionResult> DisconnectAsync(
        BluetoothConnectionRequest request,
        CancellationToken cancellationToken = default) =>
        ApplyAsync(request, enable: false, cancellationToken);

    private async Task<BluetoothConnectionResult> ApplyAsync(
        BluetoothConnectionRequest request,
        bool enable,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_radio.Current == BluetoothRadioState.Off) return BluetoothConnectionResult.RadioOff;
        if (_radio.Current != BluetoothRadioState.On) return BluetoothConnectionResult.Unavailable;
        if (string.IsNullOrWhiteSpace(request.EndpointId) && string.IsNullOrWhiteSpace(request.DeviceAddress))
            return BluetoothConnectionResult.Unavailable;

        var profile = BluetoothProfileOperationResult.Unavailable;
        var hasAddress = BluetoothAddressParser.TryParse(request.DeviceAddress, out var address);
        if (hasAddress)
        {
            profile = _profiles.SetServices(address, ServicesFor(request.DeviceType), enable);
            if (profile == BluetoothProfileOperationResult.AccessDenied)
                return BluetoothConnectionResult.AccessDenied;
            if (profile == BluetoothProfileOperationResult.Succeeded && !enable)
                return BluetoothConnectionResult.Succeeded;
        }
        else if (!enable)
            return BluetoothConnectionResult.Unavailable;

        if (enable && !string.IsNullOrWhiteSpace(request.EndpointId))
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(OperationTimeout);
            try
            {
                var acl = await _acl.ConnectAsync(request.EndpointId, timeout.Token).ConfigureAwait(false);
                if (acl == BluetoothConnectionResult.Succeeded ||
                    profile == BluetoothProfileOperationResult.Succeeded)
                    return BluetoothConnectionResult.Succeeded;
                if (acl == BluetoothConnectionResult.AccessDenied)
                    return BluetoothConnectionResult.AccessDenied;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (profile == BluetoothProfileOperationResult.Succeeded)
                    return BluetoothConnectionResult.Succeeded;
                LogFailure(enable, "timeout");
                return BluetoothConnectionResult.Failed;
            }
        }

        if (profile == BluetoothProfileOperationResult.Succeeded)
            return BluetoothConnectionResult.Succeeded;
        if (!hasAddress && string.IsNullOrWhiteSpace(request.EndpointId))
            return BluetoothConnectionResult.Unavailable;

        LogFailure(enable, "failed");
        return BluetoothConnectionResult.Failed;
    }

    private static IReadOnlyList<Guid> ServicesFor(DeviceHubDeviceType type) => type switch
    {
        DeviceHubDeviceType.Headphones or DeviceHubDeviceType.Speaker =>
            [BluetoothNative.AudioSink, BluetoothNative.Headset],
        DeviceHubDeviceType.Headset =>
            [BluetoothNative.Handsfree, BluetoothNative.Headset, BluetoothNative.AudioSink],
        DeviceHubDeviceType.Mouse or DeviceHubDeviceType.Keyboard or DeviceHubDeviceType.Gamepad =>
            [BluetoothNative.HumanInterfaceDevice],
        _ =>
        [
            BluetoothNative.AudioSink,
            BluetoothNative.Handsfree,
            BluetoothNative.Headset,
            BluetoothNative.HumanInterfaceDevice
        ]
    };

    private void LogFailure(bool enable, string reason) =>
        _log?.Write(
            TechnicalLogLevel.Warning,
            TechnicalEventIds.DeviceStatusUnavailable,
            "DeviceHub",
            "Bluetooth connection change failed safely.",
            properties: new Dictionary<string, object?>
            {
                ["operation"] = enable ? "connect" : "disconnect",
                ["reason"] = reason
            });
}
