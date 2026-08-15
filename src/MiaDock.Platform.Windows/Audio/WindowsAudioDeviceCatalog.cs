using System.Runtime.InteropServices;
using MiaDock.Modules.DeviceStatus.Models;
using MiaDock.Modules.DeviceStatus.Services;

namespace MiaDock.Platform.Windows.Audio;

/// <summary>
/// Read-only Core Audio endpoint catalog.  It intentionally does not use the
/// undocumented default-endpoint policy COM interface.
/// </summary>
public sealed class WindowsAudioDeviceCatalog : IAudioDeviceCatalog
{
    private const uint DeviceStateActive = 0x00000001;

    public Task<IReadOnlyList<AudioDeviceInfo>> GetOutputDevicesAsync(CancellationToken cancellationToken = default) =>
        EnumerateAsync(AudioDataFlow.Render, cancellationToken);

    public Task<IReadOnlyList<AudioDeviceInfo>> GetInputDevicesAsync(CancellationToken cancellationToken = default) =>
        EnumerateAsync(AudioDataFlow.Capture, cancellationToken);

    private static Task<IReadOnlyList<AudioDeviceInfo>> EnumerateAsync(
        AudioDataFlow flow,
        CancellationToken cancellationToken) =>
        Task.Run(() => Enumerate(flow, cancellationToken), cancellationToken);

    private static IReadOnlyList<AudioDeviceInfo> Enumerate(AudioDataFlow flow, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var initialized = CoreAudioNative.CoInitializeEx(0, CoreAudioNative.CoinitMultithreaded) >= 0;
        IMMDeviceEnumerator? enumerator = null;
        IMMDeviceCollection? collection = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
            var defaultConsole = GetDefaultId(enumerator, flow, AudioDeviceRole.Console);
            var defaultCommunications = GetDefaultId(enumerator, flow, AudioDeviceRole.Communications);
            CoreAudioNative.ThrowIfFailed(enumerator.EnumAudioEndpoints(flow, DeviceStateActive, out collection));
            CoreAudioNative.ThrowIfFailed(collection.GetCount(out var count));
            var result = new List<AudioDeviceInfo>((int)count);
            for (uint index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IMMDevice? device = null;
                try
                {
                    CoreAudioNative.ThrowIfFailed(collection.Item(index, out device));
                    var (id, name) = ReadIdentity(device);
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        result.Add(new AudioDeviceInfo(
                            id,
                            string.IsNullOrWhiteSpace(name) ? id : name,
                            string.Equals(id, defaultConsole, StringComparison.Ordinal),
                            string.Equals(id, defaultCommunications, StringComparison.Ordinal),
                            true));
                    }
                }
                finally
                {
                    ReleaseCom(device);
                }
            }

            return result;
        }
        finally
        {
            ReleaseCom(collection);
            ReleaseCom(enumerator);
            if (initialized) CoreAudioNative.CoUninitialize();
        }
    }

    private static string? GetDefaultId(IMMDeviceEnumerator enumerator, AudioDataFlow flow, AudioDeviceRole role)
    {
        IMMDevice? device = null;
        try
        {
            return enumerator.GetDefaultAudioEndpoint(flow, role, out device) >= 0
                ? ReadIdentity(device).Id
                : null;
        }
        catch (COMException)
        {
            return null;
        }
        finally
        {
            ReleaseCom(device);
        }
    }

    private static (string? Id, string? Name) ReadIdentity(IMMDevice device)
    {
        IPropertyStore? properties = null;
        try
        {
            if (device.GetId(out var id) < 0 || string.IsNullOrWhiteSpace(id)) return (null, null);
            if (device.OpenPropertyStore(CoreAudioNative.StorageModeRead, out properties) < 0) return (id, null);
            var key = CoreAudioNative.DeviceFriendlyNameKey;
            if (properties.GetValue(ref key, out var value) < 0) return (id, null);
            try
            {
                return (id, value.ValueType == CoreAudioNative.VariantTypeStringPointer && value.PointerValue != 0
                    ? Marshal.PtrToStringUni(value.PointerValue)?.Trim()
                    : null);
            }
            finally
            {
                CoreAudioNative.PropVariantClear(ref value);
            }
        }
        finally
        {
            ReleaseCom(properties);
        }
    }

    private static void ReleaseCom(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            try { Marshal.ReleaseComObject(value); }
            catch (InvalidComObjectException) { }
        }
    }
}
