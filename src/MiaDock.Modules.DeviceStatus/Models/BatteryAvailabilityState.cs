namespace MiaDock.Modules.DeviceStatus.Models;

public enum BatteryAvailabilityState
{
    Unknown,
    Available,
    NotPresent,
    ApiUnavailable,
    AccessDenied,
    TransientError
}
