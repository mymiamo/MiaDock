using System.Runtime.InteropServices;
using MiaDock.Core.Logging;

namespace MiaDock.Platform.Windows.Bluetooth;

internal sealed class Win32BluetoothProfileController(ILogService? log = null) : IBluetoothProfileController
{
    public BluetoothProfileOperationResult SetServices(ulong address, IReadOnlyList<Guid> services, bool enable)
    {
        if (address == 0 || services.Count == 0) return BluetoothProfileOperationResult.Unavailable;

        var parameters = new BluetoothNative.FindRadioParams
        {
            dwSize = Marshal.SizeOf<BluetoothNative.FindRadioParams>()
        };
        var find = BluetoothNative.BluetoothFindFirstRadio(ref parameters, out var radio);
        if (find == 0 || radio == 0)
        {
            if (radio != 0) BluetoothNative.CloseHandle(radio);
            if (find != 0) BluetoothNative.BluetoothFindRadioClose(find);
            return BluetoothProfileOperationResult.Unavailable;
        }

        try
        {
            var info = new BluetoothNative.DeviceInfo
            {
                dwSize = Marshal.SizeOf<BluetoothNative.DeviceInfo>(),
                Address = new BluetoothNative.Address { ullLong = address },
                szName = string.Empty
            };
            var flags = enable ? BluetoothNative.ServiceEnable : BluetoothNative.ServiceDisable;
            var succeeded = 0;
            var missing = 0;
            foreach (var service in services)
            {
                var guid = service;
                var error = BluetoothNative.BluetoothSetServiceState(radio, ref info, ref guid, flags);
                if (error == BluetoothNative.ErrorSuccess) succeeded++;
                else if (error == BluetoothNative.ErrorAccessDenied) return BluetoothProfileOperationResult.AccessDenied;
                else if (error == BluetoothNative.ErrorServiceDoesNotExist) missing++;
            }

            if (succeeded > 0) return BluetoothProfileOperationResult.Succeeded;
            if (missing == services.Count) return BluetoothProfileOperationResult.NoMatchingService;
            return BluetoothProfileOperationResult.Failed;
        }
        catch (Exception exception)
        {
            log?.Write(
                TechnicalLogLevel.Warning,
                TechnicalEventIds.DeviceStatusUnavailable,
                "DeviceHub",
                "Bluetooth profile change failed safely.",
                exception,
                new Dictionary<string, object?> { ["operation"] = enable ? "enable" : "disable" });
            return BluetoothProfileOperationResult.Failed;
        }
        finally
        {
            BluetoothNative.CloseHandle(radio);
            BluetoothNative.BluetoothFindRadioClose(find);
        }
    }
}
