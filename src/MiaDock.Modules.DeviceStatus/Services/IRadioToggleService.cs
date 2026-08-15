using MiaDock.Modules.DeviceStatus.Models;

namespace MiaDock.Modules.DeviceStatus.Services;

public interface IRadioToggleService
{
    ValueTask<RadioToggleResult> ToggleWifiAsync(CancellationToken cancellationToken = default);
    ValueTask<RadioToggleResult> ToggleBluetoothAsync(CancellationToken cancellationToken = default);
}
