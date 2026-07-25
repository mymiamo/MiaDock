using System.Runtime.InteropServices;

namespace MiaDock.Platform.Windows.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public readonly int Width => Right - Left;
    public readonly int Height => Bottom - Top;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativePoint
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LowLevelMouseHookData
{
    public NativePoint Point;
    public uint MouseData;
    public uint Flags;
    public uint Time;
    public nuint ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeMonitorInfo
{
    public uint Size;
    public NativeRect Monitor;
    public NativeRect WorkArea;
    public uint Flags;

    public static NativeMonitorInfo Create() => new()
    {
        Size = checked((uint)Marshal.SizeOf<NativeMonitorInfo>())
    };
}
