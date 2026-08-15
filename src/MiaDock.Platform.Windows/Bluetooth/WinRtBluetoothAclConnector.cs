using MiaDock.Modules.DeviceStatus.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace MiaDock.Platform.Windows.Bluetooth;

internal sealed class WinRtBluetoothAclConnector : IBluetoothAclConnector
{
    public async Task<BluetoothConnectionResult> ConnectAsync(
        string endpointId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(endpointId)) return BluetoothConnectionResult.Unavailable;

        try
        {
            if (await TryClassicAsync(endpointId, cancellationToken).ConfigureAwait(false))
                return BluetoothConnectionResult.Succeeded;
            if (await TryLowEnergyAsync(endpointId, cancellationToken).ConfigureAwait(false))
                return BluetoothConnectionResult.Succeeded;
            return BluetoothConnectionResult.Failed;
        }
        catch (UnauthorizedAccessException)
        {
            return BluetoothConnectionResult.AccessDenied;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return BluetoothConnectionResult.Failed;
        }
    }

    private static async Task<bool> TryClassicAsync(string endpointId, CancellationToken cancellationToken)
    {
        BluetoothDevice? device = null;
        try
        {
            device = await BluetoothDevice.FromIdAsync(endpointId).AsTask(cancellationToken).ConfigureAwait(false);
            if (device is null) return false;
            var services = await device.GetRfcommServicesAsync(BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);
            return services.Error is BluetoothError.Success;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            device?.Dispose();
        }
    }

    private static async Task<bool> TryLowEnergyAsync(string endpointId, CancellationToken cancellationToken)
    {
        BluetoothLEDevice? device = null;
        try
        {
            device = await BluetoothLEDevice.FromIdAsync(endpointId).AsTask(cancellationToken).ConfigureAwait(false);
            if (device is null) return false;
            var services = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken)
                .ConfigureAwait(false);
            return services.Status is GattCommunicationStatus.Success;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            device?.Dispose();
        }
    }
}
