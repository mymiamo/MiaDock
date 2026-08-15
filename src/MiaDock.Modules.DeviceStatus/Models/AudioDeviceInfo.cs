namespace MiaDock.Modules.DeviceStatus.Models;

public sealed record AudioDeviceInfo(
    string Id,
    string DisplayName,
    bool IsDefault,
    bool IsDefaultCommunications,
    bool IsActive);
