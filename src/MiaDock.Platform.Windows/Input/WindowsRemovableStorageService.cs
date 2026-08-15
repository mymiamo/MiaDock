using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Storage;
using Windows.System;

namespace MiaDock.Platform.Windows.Input;

/// <summary>Read-only removable drive catalog with Windows-owned fallback actions.</summary>
public sealed class WindowsRemovableStorageService : IRemovableStorageService
{
    private static readonly Uri ConnectedDevicesSettingsUri = new("ms-settings:connecteddevices");
    private const uint CrSuccess = 0x00000000;
    private const uint CrNoSuchDevNode = 0x0000000D;
    private const uint CrRemoveVetoed = 0x00000017;
    private const uint CrAccessDenied = 0x00000033;

    public Task<IReadOnlyList<RemovableStorageInfo>> GetDevicesAsync(CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<RemovableStorageInfo>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = new List<RemovableStorageInfo>();
            var usbRoots = GetUsbLogicalDriveInstances();
            foreach (var drive in DriveInfo.GetDrives())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var ready = drive.IsReady;
                    var root = Path.GetPathRoot(drive.Name);
                    if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root)) continue;
                    var isUsb = usbRoots.TryGetValue(root, out var deviceInstanceId);
                    if (drive.DriveType != DriveType.Removable && !isUsb) continue;
                    var label = ready ? drive.VolumeLabel?.Trim() : null;
                    var displayName = string.IsNullOrWhiteSpace(label) ? drive.Name.TrimEnd('\\') : label;
                    result.Add(new RemovableStorageInfo(
                        root.ToUpperInvariant(),
                        displayName,
                        root,
                        ready ? drive.DriveFormat : null,
                        ready ? drive.TotalSize : null,
                        ready ? drive.AvailableFreeSpace : null,
                        ready,
                        deviceInstanceId,
                        isUsb && !string.IsNullOrWhiteSpace(deviceInstanceId)));
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }

            return result;
        }, cancellationToken);

    public async Task<bool> OpenAsync(RemovableStorageInfo storage, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!storage.IsReady || !IsValidRootPath(storage.RootPath)) return false;
        try
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(storage.RootPath);
            return await Launcher.LaunchFolderAsync(folder);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or ArgumentException)
        {
            return false;
        }
    }

    public Task<RemovableStorageEjectResult> EjectAsync(
        RemovableStorageInfo storage,
        CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!storage.CanEject || string.IsNullOrWhiteSpace(storage.DeviceInstanceId))
            {
                return new RemovableStorageEjectResult(RemovableStorageEjectStatus.Unsupported);
            }

            var locateResult = CM_Locate_DevNodeW(out var deviceInstance, storage.DeviceInstanceId, 0);
            if (locateResult != CrSuccess)
            {
                return new RemovableStorageEjectResult(
                    locateResult == CrNoSuchDevNode
                        ? RemovableStorageEjectStatus.NotFound
                        : locateResult == CrAccessDenied
                            ? RemovableStorageEjectStatus.AccessDenied
                            : RemovableStorageEjectStatus.Failed,
                    locateResult);
            }

            var vetoName = new StringBuilder(260);
            var result = CM_Request_Device_EjectW(
                deviceInstance,
                out _,
                vetoName,
                (uint)vetoName.Capacity,
                0);
            return new RemovableStorageEjectResult(
                result switch
                {
                    CrSuccess => RemovableStorageEjectStatus.Succeeded,
                    CrRemoveVetoed => RemovableStorageEjectStatus.InUse,
                    CrAccessDenied => RemovableStorageEjectStatus.AccessDenied,
                    CrNoSuchDevNode => RemovableStorageEjectStatus.NotFound,
                    _ => RemovableStorageEjectStatus.Failed
                },
                result);
        }, cancellationToken);

    public async Task<bool> OpenSafelyRemoveHardwareAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { return await Launcher.LaunchUriAsync(ConnectedDevicesSettingsUri); }
        catch (Exception exception) when (exception is UnauthorizedAccessException or InvalidOperationException) { return false; }
    }

    private static bool IsValidRootPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) return false;
        var root = Path.GetPathRoot(path);
        return string.Equals(path, root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(path);
    }

    private static Dictionary<string, string?> GetUsbLogicalDriveInstances()
    {
        var roots = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var diskDrives = new ManagementObjectSearcher(
                "SELECT DeviceID, PNPDeviceID FROM Win32_DiskDrive WHERE InterfaceType = 'USB'").Get();
            foreach (ManagementObject disk in diskDrives)
            {
                var diskId = disk["DeviceID"] as string;
                var deviceInstanceId = disk["PNPDeviceID"] as string;
                if (string.IsNullOrWhiteSpace(diskId)) continue;
                foreach (ManagementObject partition in Associators(diskId, "Win32_DiskDrive", "Win32_DiskDriveToDiskPartition"))
                {
                    var partitionId = partition["DeviceID"] as string;
                    if (string.IsNullOrWhiteSpace(partitionId)) continue;
                    foreach (ManagementObject logicalDisk in Associators(partitionId, "Win32_DiskPartition", "Win32_LogicalDiskToPartition"))
                    {
                        if (logicalDisk["DeviceID"] is string letter && letter.Length == 2 && letter[1] == ':')
                        {
                            roots[$"{letter}\\"] = deviceInstanceId;
                        }
                    }
                }
            }
        }
        catch (ManagementException) { }
        catch (UnauthorizedAccessException) { }

        return roots;
    }

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Locate_DevNodeW(
        out uint deviceInstance,
        string deviceInstanceId,
        uint flags);

    [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Request_Device_EjectW(
        uint deviceInstance,
        out PnpVetoType vetoType,
        StringBuilder vetoName,
        uint nameLength,
        uint flags);

    private enum PnpVetoType
    {
        Unknown,
        LegacyDevice,
        PendingClose,
        WindowsApp,
        WindowsService,
        OutstandingOpen,
        Device,
        Driver,
        IllegalDeviceRequest,
        InsufficientPower,
        NonDisableable,
        LegacyDriver,
        InsufficientRights,
        AlreadyRemoved
    }

    private static IEnumerable<ManagementObject> Associators(
        string deviceId,
        string className,
        string associationClass)
    {
        var escapedId = deviceId.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("'", "''", StringComparison.Ordinal);
        var query = $"ASSOCIATORS OF {{{className}.DeviceID='{escapedId}'}} WHERE AssocClass={associationClass}";
        using var searcher = new ManagementObjectSearcher(query);
        using var result = searcher.Get();
        foreach (ManagementObject item in result)
        {
            yield return item;
        }
    }
}
