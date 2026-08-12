using MiaDock.Modules.DeviceStatus.Models;
using Windows.Devices.Radios;

namespace MiaDock.Platform.Windows.Bluetooth;

public sealed class WindowsBluetoothRadioStateProvider : IBluetoothRadioStateProvider
{
    private Radio? _radio;
    private bool _started;
    private bool _disposed;

    public BluetoothRadioState Current { get; private set; } = BluetoothRadioState.Unknown;

    public event EventHandler<BluetoothRadioState>? StateChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return;
        }
        _started = true;

        try
        {
            var radios = await Radio.GetRadiosAsync().AsTask(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            _radio = radios.FirstOrDefault(radio => radio.Kind == RadioKind.Bluetooth);
            if (_radio is null)
            {
                Publish(BluetoothRadioState.Unavailable);
                return;
            }
            _radio.StateChanged += OnRadioStateChanged;
            Publish(Map(_radio.State));
        }
        catch (OperationCanceledException)
        {
            _started = false;
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            Publish(BluetoothRadioState.Unavailable);
        }
        catch (Exception)
        {
            Publish(BluetoothRadioState.Unavailable);
        }
    }

    public void Stop()
    {
        if (_radio is not null)
        {
            _radio.StateChanged -= OnRadioStateChanged;
            _radio = null;
        }
        _started = false;
        Current = BluetoothRadioState.Unknown;
    }

    private void OnRadioStateChanged(Radio sender, object args)
    {
        if (_disposed || !_started || !ReferenceEquals(sender, _radio))
        {
            return;
        }
        Publish(Map(sender.State));
    }

    private void Publish(BluetoothRadioState state)
    {
        if (state == Current)
        {
            return;
        }
        Current = state;
        StateChanged?.Invoke(this, state);
    }

    internal static BluetoothRadioState Map(RadioState state) => state switch
    {
        RadioState.On => BluetoothRadioState.On,
        RadioState.Off => BluetoothRadioState.Off,
        RadioState.Disabled => BluetoothRadioState.Off,
        _ => BluetoothRadioState.Unknown
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        Stop();
        _disposed = true;
    }
}
