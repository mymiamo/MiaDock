namespace MiaDock.Modules.DeviceStatus.Models;

public enum NetworkConnectivityKind
{
    Offline,
    LocalAccess,
    ConstrainedInternet,
    Internet
}

public enum NetworkConnectionKind
{
    None,
    WiFi,
    Ethernet,
    Cellular,
    Other
}

public enum NetworkThroughputState
{
    Inactive,
    Sampling,
    Ready,
    Unavailable
}

public sealed record NetworkStatusSnapshot(
    DeviceServiceState State,
    NetworkConnectivityKind Connectivity,
    NetworkConnectionKind ConnectionKind,
    bool IsMetered,
    Guid? AdapterId,
    double? DownloadBytesPerSecond,
    double? UploadBytesPerSecond,
    NetworkThroughputState ThroughputState = NetworkThroughputState.Inactive,
    bool IsVpnActive = false)
{
    public static NetworkStatusSnapshot Default { get; } = new(
        DeviceServiceState.Stopped,
        NetworkConnectivityKind.Offline,
        NetworkConnectionKind.None,
        false,
        null,
        null,
        null);
}
