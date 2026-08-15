namespace MiaDock.Modules.DeviceStatus.Models;

public enum BluetoothConnectionResult
{
    Succeeded,
    Unavailable,
    AccessDenied,
    RadioOff,
    Failed
}

public sealed record BluetoothConnectionRequest(
    string? EndpointId,
    string? DeviceAddress,
    DeviceHubDeviceType DeviceType);
