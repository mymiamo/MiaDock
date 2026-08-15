namespace MiaDock.Modules.DeviceStatus.Models;

public sealed record RemovableStorageInfo(
    string Id,
    string DisplayName,
    string RootPath,
    string? FileSystem,
    long? TotalSpace,
    long? FreeSpace,
    bool IsReady,
    string? DeviceInstanceId = null,
    bool CanEject = false);

public enum RemovableStorageEjectStatus
{
    Succeeded,
    InUse,
    AccessDenied,
    NotFound,
    Unsupported,
    Failed
}

public sealed record RemovableStorageEjectResult(
    RemovableStorageEjectStatus Status,
    uint NativeCode = 0)
{
    public bool Succeeded => Status == RemovableStorageEjectStatus.Succeeded;
}
