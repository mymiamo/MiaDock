using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using MiaDock.Core.Input;

namespace MiaDock.Platform.Windows.Input;

public sealed class WindowsUsbDeviceMonitor : IUsbDeviceMonitor
{
    private const uint WmDeviceChange = 0x0219;
    private const nuint DbtDeviceArrival = 0x8000;
    private const nuint DbtDeviceRemoveComplete = 0x8004;
    private const uint DbtDevTypVolume = 0x00000002;
    private static readonly nint HwndMessage = new(-3);

    private readonly WindowProcedure _windowProcedure;
    private readonly string _windowClassName = $"MiaDock.UsbDevices.{Guid.NewGuid():N}";
    private readonly Dictionary<char, string> _knownLabels = new();
    private readonly object _gate = new();
    private readonly Timer _reconciliationTimer;
    private nint _instance;
    private nint _windowHandle;
    private bool _running;
    private bool _disposed;

    public WindowsUsbDeviceMonitor()
    {
        _windowProcedure = HandleWindowMessage;
        _reconciliationTimer = new Timer(
            _ => ReconcileRemovableDrivesSafely(),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
    }

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

    public ValueTask StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            if (_running)
            {
                return ValueTask.CompletedTask;
            }

            EnsureWindow();
            SeedKnownRemovableDrives();
            _running = true;
            _reconciliationTimer.Change(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _running = false;
            _knownLabels.Clear();
            _reconciliationTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        // Skip DestroyWindow/UnregisterClass: nested WndProc during teardown can
        // fail-fast. Process exit reclaims the message-only HWND.
        _disposed = true;
        lock (_gate)
        {
            _running = false;
            _windowHandle = 0;
            _knownLabels.Clear();
            _reconciliationTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        _reconciliationTimer.Dispose();

        return ValueTask.CompletedTask;
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

        _windowHandle = CreateWindowExW(
            0,
            _windowClassName,
            string.Empty,
            0,
            0,
            0,
            0,
            0,
            HwndMessage,
            0,
            _instance,
            0);
        if (_windowHandle == 0)
        {
            var error = Marshal.GetLastWin32Error();
            UnregisterClassW(_windowClassName, _instance);
            throw new Win32Exception(error, "USB mesaj penceresi oluşturulamadı.");
        }
    }

    private void SeedKnownRemovableDrives()
    {
        _knownLabels.Clear();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!IsRemovableDrive(drive))
            {
                continue;
            }

            var letter = char.ToUpperInvariant(drive.Name[0]);
            _knownLabels[letter] = ResolveDisplayName(drive, letter);
        }
    }

    private nint HandleWindowMessage(nint window, uint message, nuint wParam, nint lParam)
    {
        try
        {
            if (!_disposed &&
                message == WmDeviceChange &&
                (wParam == DbtDeviceArrival || wParam == DbtDeviceRemoveComplete) &&
                lParam != 0)
            {
                // A message-only HWND does not receive every volume broadcast on
                // all Windows builds. Reconcile on the worker-pool timer instead
                // of touching drives or subscribers on this native callback stack.
                _reconciliationTimer.Change(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(2));
                return 0;
            }
        }
        catch (Exception)
        {
            // A managed exception must never cross the reverse P/Invoke WndProc
            // boundary; escaping here terminates the process through a native
            // fail-fast path that no handler can observe.
        }

        return DefWindowProcW(window, message, wParam, lParam);
    }

    private void HandleDeviceChange(bool connected, nint lParam)
    {
        bool running;
        lock (_gate)
        {
            running = _running && !_disposed;
        }

        if (!running)
        {
            return;
        }

        var header = Marshal.PtrToStructure<DevBroadcastHeader>(lParam);
        if (header.DeviceType != DbtDevTypVolume)
        {
            return;
        }

        var volume = Marshal.PtrToStructure<DevBroadcastVolume>(lParam);
        foreach (var letter in EnumerateDriveLetters(volume.UnitMask))
        {
            RaiseForDrive(connected, letter);
        }
    }

    private void RaiseForDrive(bool connected, char letter)
    {
        if (connected && !TryRaiseConnected(letter))
        {
            _ = RetryConnectedAsync(letter);
            return;
        }

        if (!connected)
        {
            TryRaiseDisconnected(letter);
        }
    }

    private async Task RetryConnectedAsync(char letter)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                await Task.Delay(250).ConfigureAwait(false);
            }
            catch
            {
                return;
            }

            if (TryRaiseConnected(letter))
            {
                return;
            }
        }
    }

    private bool TryRaiseConnected(char letter)
    {
        string displayName;
        lock (_gate)
        {
            if (!_running || _disposed)
            {
                return true;
            }

            if (!TryGetRemovableDrive(letter, out var drive))
            {
                return false;
            }

            displayName = ResolveDisplayName(drive!, letter);
            _knownLabels[letter] = displayName;
        }

        PublishDeviceChanged(true, letter, displayName);
        return true;
    }

    private void TryRaiseDisconnected(char letter)
    {
        string displayName;
        lock (_gate)
        {
            if (!_running || _disposed)
            {
                return;
            }

            if (!_knownLabels.Remove(letter, out displayName!))
            {
                return;
            }
        }

        PublishDeviceChanged(false, letter, displayName);
    }

    private void ReconcileRemovableDrivesSafely()
    {
        try
        {
            Dictionary<char, string> current = [];
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!IsRemovableDrive(drive)) continue;
                var letter = char.ToUpperInvariant(drive.Name[0]);
                current[letter] = ResolveDisplayName(drive, letter);
            }

            List<(bool Connected, char Letter, string DisplayName)> changes = [];
            lock (_gate)
            {
                if (!_running || _disposed) return;
                foreach (var (letter, label) in _knownLabels)
                {
                    if (!current.ContainsKey(letter)) changes.Add((false, letter, label));
                }

                foreach (var (letter, label) in current)
                {
                    if (!_knownLabels.ContainsKey(letter)) changes.Add((true, letter, label));
                }

                _knownLabels.Clear();
                foreach (var (letter, label) in current) _knownLabels[letter] = label;
            }

            foreach (var change in changes)
            {
                PublishDeviceChanged(change.Connected, change.Letter, change.DisplayName);
            }
        }
        catch
        {
            // USB enumeration must not take down the process during device churn.
        }
    }

    private void PublishDeviceChanged(bool connected, char letter, string displayName)
    {
        foreach (EventHandler<UsbDeviceChangedEventArgs> handler in DeviceChanged?.GetInvocationList() ?? [])
        {
            try
            {
                handler(this, new UsbDeviceChangedEventArgs(
                    connected, $"{letter}:", displayName, DateTimeOffset.UtcNow));
            }
            catch
            {
                // Device subscribers can be shutting down concurrently.
            }
        }
    }

    private static bool TryGetRemovableDrive(char letter, out DriveInfo? drive)
    {
        try
        {
            drive = new DriveInfo($"{letter}:\\");
            if (IsRemovableDrive(drive))
            {
                return true;
            }
        }
        catch
        {
            // Drive may still be mounting.
        }

        drive = null;
        return false;
    }

    private static string ResolveDisplayName(DriveInfo drive, char letter)
    {
        try
        {
            if (drive.IsReady)
            {
                var label = drive.VolumeLabel?.Trim();
                if (!string.IsNullOrEmpty(label))
                {
                    return $"{label} ({letter}:)";
                }
            }
        }
        catch
        {
            // Volume metadata can throw while the device is still settling.
        }

        return $"{letter}:";
    }

    private static bool IsRemovableDrive(DriveInfo drive)
    {
        try
        {
            return drive.DriveType == DriveType.Removable;
        }
        catch
        {
            return false;
        }
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

    [StructLayout(LayoutKind.Sequential)]
    private struct DevBroadcastHeader
    {
        public uint Size;
        public uint DeviceType;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DevBroadcastVolume
    {
        public uint Size;
        public uint DeviceType;
        public uint Reserved;
        public uint UnitMask;
        public ushort Flags;
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
    private static extern nint CreateWindowExW(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        nint parent,
        nint menu,
        nint instance,
        nint parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterClassW(string className, nint instance);

    [DllImport("user32.dll")]
    private static extern nint DefWindowProcW(nint window, uint message, nuint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandleW(string? moduleName);
}
