using MiaDock.Core.Logging;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;
using Windows.Devices.Radios;

namespace MiaDock.Platform.Windows.Connectivity;

public sealed class WindowsRadioToggleService(ILogService? log = null) : IRadioToggleService
{
    public ValueTask<RadioToggleResult> ToggleWifiAsync(CancellationToken cancellationToken = default) =>
        ToggleAsync(RadioKind.WiFi, "wifi", cancellationToken);

    public ValueTask<RadioToggleResult> ToggleBluetoothAsync(CancellationToken cancellationToken = default) =>
        ToggleAsync(RadioKind.Bluetooth, "bluetooth", cancellationToken);

    private async ValueTask<RadioToggleResult> ToggleAsync(
        RadioKind kind,
        string radioName,
        CancellationToken cancellationToken)
    {
        try
        {
            var radios = await Radio.GetRadiosAsync().AsTask(cancellationToken);
            var radio = radios.FirstOrDefault(candidate => candidate.Kind == kind);
            if (radio is null) return RadioToggleResult.Unavailable;

            var target = radio.State == RadioState.On ? RadioState.Off : RadioState.On;
            var result = await radio.SetStateAsync(target).AsTask(cancellationToken);
            return result.ToString() switch
            {
                "Allowed" => RadioToggleResult.Succeeded,
                "DeniedByUser" or "DeniedBySystem" => RadioToggleResult.AccessDenied,
                _ => RadioToggleResult.Failed
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            log?.Write(TechnicalLogLevel.Warning, "radio.toggle.denied", "Connectivity",
                "A radio toggle request was denied.", exception, new Dictionary<string, object?> { ["radio"] = radioName });
            return RadioToggleResult.AccessDenied;
        }
        catch (Exception exception)
        {
            log?.Write(TechnicalLogLevel.Warning, "radio.toggle.failed", "Connectivity",
                "A radio toggle request failed safely.", exception, new Dictionary<string, object?> { ["radio"] = radioName });
            return RadioToggleResult.Failed;
        }
    }
}
