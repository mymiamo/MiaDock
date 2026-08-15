namespace MiaDock.Core.Input;

public sealed record UsbDeviceChangedEventArgs(
    bool IsConnected,
    string DriveLetter,
    string DisplayName,
    DateTimeOffset OccurredAtUtc,
    string DeviceKey = "");
