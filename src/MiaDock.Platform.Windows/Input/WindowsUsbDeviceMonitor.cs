using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using MiaDock.Core.Input;

namespace MiaDock.Platform.Windows.Input;

/// <summary>
/// Observes physical USB devices through Windows device-interface notifications.
/// Drive enumeration is deliberately used only to enrich a newly-arrived storage
/// device; it is not the source of truth for USB arrival or removal.
/// </summary>
public sealed class WindowsUsbDeviceMonitor : IUsbDeviceMonitor
{
    private const uint WmDeviceChange = 0x0219;
    private const nuint DbtDeviceArrival = 0x8000;
    private const nuint DbtDeviceRemoveComplete = 0x8004;
    private const uint DbtDevTypDeviceInterface = 0x00000005;
    private const uint DeviceNotifyWindowHandle = 0x00000000;
    private const int DeviceInterfaceNameOffset = 28;
    private static readonly Guid UsbDeviceInterfaceClass = new("A5DCBF10-6530-11D2-901F-00C04FB951ED");
    private static readonly nint HwndMessage = new(-3);

    private readonly WindowProcedure _windowProcedure;
    private readonly string _windowClassName = $"MiaDock.UsbDevices.{Guid.NewGuid():N}";
    private readonly object _gate = new();
    private readonly UsbDeviceChangeCoalescer _coalescer = new(TimeSpan.FromSeconds(2));
    private readonly Dictionary<string, StorageVolume> _knownStorageVolumes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DevicePresentation> _knownDevices = new(StringComparer.Ordinal);
    private readonly HashSet<string> _connectedDeviceKeys = new(StringComparer.Ordinal);
    private nint _instance;
    private nint _windowHandle;
    private nint _deviceNotification;
    private int _monitorSession;
    private int _leaseCount;
    private bool _running;
    private bool _disposed;

    public WindowsUsbDeviceMonitor() => _windowProcedure = HandleWindowMessage;

    public event EventHandler<UsbDeviceChangedEventArgs>? DeviceChanged;

    public bool IsRunning
    {
        get
        {
            lock (_gate)
            {
                return _running;
            }
        }
    }

    public ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _leaseCount++;
            if (_leaseCount > 1)
            {
                return ValueTask.FromResult<IAsyncDisposable>(new Lease(this));
            }

            try
            {
                EnsureWindow();
                SeedKnownStorageVolumes(); // Initial inventory is intentionally silent.
                _knownDevices.Clear();
                _connectedDeviceKeys.Clear();
                _coalescer.Reset();
                _monitorSession++;
                _running = true;
            }
            catch
            {
                _leaseCount--;
                throw;
            }
        }

        return ValueTask.FromResult<IAsyncDisposable>(new Lease(this));
    }

    private ValueTask ReleaseLeaseAsync()
    {
        lock (_gate)
        {
            if (_leaseCount == 0)
            {
                return ValueTask.CompletedTask;
            }

            _leaseCount--;
            if (_leaseCount > 0)
            {
                return ValueTask.CompletedTask;
            }

            _running = false;
            _monitorSession++;
            _knownStorageVolumes.Clear();
            _knownDevices.Clear();
            _connectedDeviceKeys.Clear();
            _coalescer.Reset();
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        nint notification;
        lock (_gate)
        {
            _disposed = true;
            _leaseCount = 0;
            _running = false;
            _monitorSession++;
            _knownStorageVolumes.Clear();
            _knownDevices.Clear();
            _connectedDeviceKeys.Clear();
            _coalescer.Reset();
            notification = _deviceNotification;
            _deviceNotification = 0;
            _windowHandle = 0;
        }

        // Never let native cleanup destabilise app shutdown. The message-only
        // window is process-owned and is reclaimed by Windows at process exit.
        if (notification != 0)
        {
            try { _ = UnregisterDeviceNotification(notification); }
            catch { }
        }

        return ValueTask.CompletedTask;
    }

    private sealed class Lease(WindowsUsbDeviceMonitor owner) : IAsyncDisposable
    {
        private WindowsUsbDeviceMonitor? _owner = owner;

        public ValueTask DisposeAsync()
        {
            var current = Interlocked.Exchange(ref _owner, null);
            return current is null ? ValueTask.CompletedTask : current.ReleaseLeaseAsync();
        }
    }

    private void EnsureWindow()
    {
        if (_windowHandle != 0)
        {
            return;
        }

        _instance = GetModuleHandleW(null);
        var windowClass = new WindowClass
        {
            Size = (uint)Marshal.SizeOf<WindowClass>(),
            Instance = _instance,
            WindowProcedure = _windowProcedure,
            ClassName = _windowClassName
        };
        if (RegisterClassExW(ref windowClass) == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "USB mesaj penceresi kaydedilemedi.");
        }

        _windowHandle = CreateWindowExW(0, _windowClassName, string.Empty, 0, 0, 0, 0, 0,
            HwndMessage, 0, _instance, 0);
        if (_windowHandle == 0)
        {
            var error = Marshal.GetLastWin32Error();
            UnregisterClassW(_windowClassName, _instance);
            throw new Win32Exception(error, "USB mesaj penceresi oluşturulamadı.");
        }

        var filter = new DevBroadcastDeviceInterface
        {
            Size = Marshal.SizeOf<DevBroadcastDeviceInterface>(),
            DeviceType = DbtDevTypDeviceInterface,
            ClassGuid = UsbDeviceInterfaceClass
        };
        _deviceNotification = RegisterDeviceNotificationW(_windowHandle, ref filter, DeviceNotifyWindowHandle);
        if (_deviceNotification == 0)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "USB cihaz bildirimleri kaydedilemedi.");
        }
    }

    private nint HandleWindowMessage(nint window, uint message, nuint wParam, nint lParam)
    {
        try
        {
            if (!_disposed && message == WmDeviceChange && lParam != 0 &&
                (wParam == DbtDeviceArrival || wParam == DbtDeviceRemoveComplete))
            {
                HandleDeviceChange(wParam == DbtDeviceArrival, lParam);
                return 0;
            }
        }
        catch
        {
            // A managed exception may never cross this reverse P/Invoke boundary.
        }

        return DefWindowProcW(window, message, wParam, lParam);
    }

    private void HandleDeviceChange(bool connected, nint lParam)
    {
        if (!TryReadUsbInterfaceKey(lParam, out var deviceKey))
        {
            return;
        }

        int session;
        DevicePresentation removedPresentation = default;
        lock (_gate)
        {
            if (!_running || _disposed || !_coalescer.TryAccept(deviceKey, connected, DateTimeOffset.UtcNow))
            {
                return;
            }

            session = _monitorSession;
            if (connected)
            {
                _connectedDeviceKeys.Add(deviceKey);
            }
            else
            {
                _connectedDeviceKeys.Remove(deviceKey);
                if (_knownDevices.Remove(deviceKey, out var known))
                {
                    removedPresentation = known;
                }
            }
        }

        if (connected)
        {
            _ = PublishConnectedAfterDeviceSettlesAsync(deviceKey, session);
            return;
        }

        PublishDeviceChanged(false, deviceKey,
            removedPresentation == default ? DevicePresentation.Generic : removedPresentation);
    }

    private async Task PublishConnectedAfterDeviceSettlesAsync(string deviceKey, int session)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(350)).ConfigureAwait(false);

            DevicePresentation presentation;
            lock (_gate)
            {
                if (!_running || _disposed || session != _monitorSession || !_connectedDeviceKeys.Contains(deviceKey))
                {
                    return;
                }

                presentation = FindNewStorageVolume() ?? DevicePresentation.Generic;
                _knownDevices[deviceKey] = presentation;
            }

            PublishDeviceChanged(true, deviceKey, presentation);
        }
        catch
        {
            // Device settling and volume metadata are optional enrichment only.
        }
    }

    private DevicePresentation? FindNewStorageVolume()
    {
        var current = CaptureStorageVolumes();
        var added = current.FirstOrDefault(pair => !_knownStorageVolumes.ContainsKey(pair.Key));
        _knownStorageVolumes.Clear();
        foreach (var pair in current)
        {
            _knownStorageVolumes[pair.Key] = pair.Value;
        }

        return string.IsNullOrEmpty(added.Key) ? null : added.Value.ToPresentation();
    }

    private void SeedKnownStorageVolumes()
    {
        _knownStorageVolumes.Clear();
        foreach (var pair in CaptureStorageVolumes())
        {
            _knownStorageVolumes[pair.Key] = pair.Value;
        }
    }

    private static Dictionary<string, StorageVolume> CaptureStorageVolumes()
    {
        var volumes = new Dictionary<string, StorageVolume>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.DriveType is not (DriveType.Fixed or DriveType.Removable) || !drive.IsReady)
                    {
                        continue;
                    }

                    var root = drive.Name;
                    if (string.IsNullOrWhiteSpace(root) || root.Length < 2)
                    {
                        continue;
                    }

                    var letter = char.ToUpperInvariant(root[0]) + ":";
                    volumes[root] = new StorageVolume(letter, drive.VolumeLabel);
                }
                catch
                {
                    // A volume can disappear during enumeration.
                }
            }
        }
        catch
        {
            // Drive queries are not required for USB device detection.
        }

        return volumes;
    }

    private bool TryReadUsbInterfaceKey(nint lParam, out string deviceKey)
    {
        deviceKey = string.Empty;
        try
        {
            var header = Marshal.PtrToStructure<DevBroadcastHeader>(lParam);
            if (header.DeviceType != DbtDevTypDeviceInterface || header.Size < DeviceInterfaceNameOffset + sizeof(char))
            {
                return false;
            }

            var deviceInterface = Marshal.PtrToStructure<DevBroadcastDeviceInterfaceHeader>(lParam);
            if (deviceInterface.ClassGuid != UsbDeviceInterfaceClass)
            {
                return false;
            }

            var byteLength = Math.Min((int)header.Size - DeviceInterfaceNameOffset, 32 * 1024);
            var rawPath = Marshal.PtrToStringUni(lParam + DeviceInterfaceNameOffset, byteLength / sizeof(char))?.TrimEnd('\0');
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return false;
            }

            // The raw interface path can contain hardware identifiers. Retain only
            // a short one-way key and never log or expose the original path.
            deviceKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawPath)))[..16];
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void PublishDeviceChanged(bool connected, string deviceKey, DevicePresentation presentation)
    {
        foreach (EventHandler<UsbDeviceChangedEventArgs> handler in DeviceChanged?.GetInvocationList() ?? [])
        {
            try
            {
                handler(this, CreateDeviceChangedEvent(
                    connected, deviceKey, presentation.DriveLetter, presentation.DisplayName, DateTimeOffset.UtcNow));
            }
            catch
            {
                // Subscribers may be shutting down concurrently.
            }
        }
    }

    internal static UsbDeviceChangedEventArgs CreateDeviceChangedEvent(
        bool connected,
        string deviceKey,
        string? driveLetter,
        string? volumeLabel,
        DateTimeOffset occurredAtUtc)
    {
        var normalizedDrive = string.IsNullOrWhiteSpace(driveLetter) ? string.Empty : driveLetter.Trim();
        var label = volumeLabel?.Trim();
        var displayName = string.IsNullOrEmpty(normalizedDrive)
            ? "USB device"
            : string.IsNullOrEmpty(label) || string.Equals(label, normalizedDrive, StringComparison.OrdinalIgnoreCase)
                ? normalizedDrive
                : $"{label} ({normalizedDrive})";

        return new UsbDeviceChangedEventArgs(connected, normalizedDrive, displayName, occurredAtUtc, deviceKey);
    }

    internal static IEnumerable<char> EnumerateDriveLetters(uint unitMask)
    {
        for (var index = 0; index < 26; index++)
        {
            if ((unitMask & (1u << index)) != 0)
            {
                yield return (char)('A' + index);
            }
        }
    }

    private readonly record struct StorageVolume(string DriveLetter, string? Label)
    {
        public DevicePresentation ToPresentation() => new(DriveLetter, Label);
    }

    private readonly record struct DevicePresentation(string DriveLetter, string? DisplayName)
    {
        public static DevicePresentation Generic { get; } = new(string.Empty, null);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DevBroadcastHeader
    {
        public uint Size;
        public uint DeviceType;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DevBroadcastDeviceInterfaceHeader
    {
        public uint Size;
        public uint DeviceType;
        public uint Reserved;
        public Guid ClassGuid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DevBroadcastDeviceInterface
    {
        public int Size;
        public uint DeviceType;
        public uint Reserved;
        public Guid ClassGuid;
        public ushort Name;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowClass
    {
        public uint Size;
        public uint Style;
        public WindowProcedure WindowProcedure;
        public int ClassExtra;
        public int WindowExtra;
        public nint Instance;
        public nint Icon;
        public nint Cursor;
        public nint Background;
        public string? MenuName;
        public string ClassName;
        public nint SmallIcon;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint WindowProcedure(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern ushort RegisterClassExW(ref WindowClass windowClass);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateWindowExW(uint extendedStyle, string className, string windowName,
        uint style, int x, int y, int width, int height, nint parent, nint menu, nint instance, nint parameter);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint RegisterDeviceNotificationW(nint recipient, ref DevBroadcastDeviceInterface notificationFilter, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterDeviceNotification(nint handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClassW(string className, nint instance);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProcW(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? moduleName);
}

internal sealed class UsbDeviceChangeCoalescer(TimeSpan duplicateWindow)
{
    private readonly TimeSpan _duplicateWindow = duplicateWindow;
    private readonly Dictionary<string, (bool Connected, DateTimeOffset OccurredAtUtc)> _events = new(StringComparer.Ordinal);

    public bool TryAccept(string deviceKey, bool connected, DateTimeOffset occurredAtUtc)
    {
        if (_events.TryGetValue(deviceKey, out var previous) &&
            previous.Connected == connected &&
            occurredAtUtc - previous.OccurredAtUtc < _duplicateWindow)
        {
            return false;
        }

        _events[deviceKey] = (connected, occurredAtUtc);
        return true;
    }

    public void Reset() => _events.Clear();
}
