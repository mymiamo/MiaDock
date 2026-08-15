using System.Runtime.InteropServices;

namespace MiaDock.Platform.Windows.Bluetooth;

internal static class BluetoothNative
{
    public const int ErrorSuccess = 0;
    public const int ErrorAccessDenied = 5;
    public const int ErrorServiceDoesNotExist = 1060;
    public const uint ServiceEnable = 0x00000001;
    public const uint ServiceDisable = 0x00000000;
    public const int DeviceInfoNameLength = 248;

    public static readonly Guid AudioSink = new("0000110b-0000-1000-8000-00805f9b34fb");
    public static readonly Guid Handsfree = new("0000111e-0000-1000-8000-00805f9b34fb");
    public static readonly Guid Headset = new("00001108-0000-1000-8000-00805f9b34fb");
    public static readonly Guid HumanInterfaceDevice = new("00001124-0000-1000-8000-00805f9b34fb");

    [StructLayout(LayoutKind.Sequential)]
    public struct FindRadioParams
    {
        public int dwSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Address
    {
        public ulong ullLong;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SystemTime
    {
        public ushort wYear;
        public ushort wMonth;
        public ushort wDayOfWeek;
        public ushort wDay;
        public ushort wHour;
        public ushort wMinute;
        public ushort wSecond;
        public ushort wMilliseconds;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DeviceInfo
    {
        public int dwSize;
        public Address Address;
        public uint ulClassofDevice;
        [MarshalAs(UnmanagedType.Bool)] public bool fConnected;
        [MarshalAs(UnmanagedType.Bool)] public bool fRemembered;
        [MarshalAs(UnmanagedType.Bool)] public bool fAuthenticated;
        public SystemTime stLastSeen;
        public SystemTime stLastUsed;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = DeviceInfoNameLength)]
        public string szName;
    }

    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    public static extern nint BluetoothFindFirstRadio(ref FindRadioParams pbtfrp, out nint phRadio);

    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool BluetoothFindRadioClose(nint hFind);

    [DllImport("BluetoothAPIs.dll", SetLastError = true)]
    public static extern uint BluetoothSetServiceState(
        nint hRadio,
        ref DeviceInfo pbtdi,
        ref Guid pGuidService,
        uint dwServiceFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(nint hObject);
}
