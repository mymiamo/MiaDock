using System.ComponentModel;
using System.Runtime.InteropServices;
using MiaDock.Core.Overlay;
using MiaDock.Platform.Windows.Interop;

namespace MiaDock.Platform.Windows.Overlay;

internal sealed class LayeredRoundedBackdropWindow : IDisposable
{
    private const uint WsPopup = 0x80000000;
    private const uint WsExTopmost = 0x00000008;
    private const uint WsExTransparent = 0x00000020;
    private const uint WsExToolWindow = 0x00000080;
    private const uint WsExLayered = 0x00080000;
    private const uint WsExNoActivate = 0x08000000;
    private const uint DibRgbColors = 0;
    private const uint BiRgb = 0;
    private const byte AcSrcOver = 0;
    private const byte AcSrcAlpha = 1;
    private const uint UlwAlpha = 0x00000002;

    private nint _windowHandle;
    private bool _disposed;

    internal LayeredRoundedBackdropWindow()
    {
        _windowHandle = CreateWindowExW(
            WsExTopmost |
            WsExTransparent |
            WsExToolWindow |
            WsExLayered |
            WsExNoActivate,
            "STATIC",
            string.Empty,
            WsPopup,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);
        if (_windowHandle == 0)
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Unable to create the anti-aliased overlay surface.");
        }
    }

    internal void Update(
        OverlayPlacement placement,
        double radius,
        double edgeThickness,
        uint argb,
        nint mainWindow,
        bool visible)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var pixels = RoundedRectangleRasterizer.RenderPremultipliedBgra(
            placement.Width,
            placement.Height,
            radius,
            argb,
            edgeThickness);
        var screenDc = GetDC(0);
        if (screenDc == 0)
        {
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Unable to acquire the desktop device context.");
        }

        var memoryDc = CreateCompatibleDC(screenDc);
        if (memoryDc == 0)
        {
            _ = ReleaseDC(0, screenDc);
            throw new Win32Exception(
                Marshal.GetLastPInvokeError(),
                "Unable to create the overlay memory device context.");
        }

        nint bitmap = 0;
        nint previousBitmap = 0;
        try
        {
            var bitmapInfo = BitmapInfo.Create(placement.Width, placement.Height);
            bitmap = CreateDIBSection(
                memoryDc,
                ref bitmapInfo,
                DibRgbColors,
                out var bitmapBits,
                0,
                0);
            if (bitmap == 0 || bitmapBits == 0)
            {
                throw new Win32Exception(
                    Marshal.GetLastPInvokeError(),
                    "Unable to create the anti-aliased overlay bitmap.");
            }

            Marshal.Copy(pixels, 0, bitmapBits, pixels.Length);
            previousBitmap = SelectObject(memoryDc, bitmap);

            var destination = new NativePoint
            {
                X = placement.X,
                Y = placement.Y
            };
            var size = new NativeSize
            {
                Width = placement.Width,
                Height = placement.Height
            };
            var source = new NativePoint();
            var blend = new NativeBlendFunction
            {
                BlendOperation = AcSrcOver,
                SourceConstantAlpha = byte.MaxValue,
                AlphaFormat = AcSrcAlpha
            };

            if (!UpdateLayeredWindow(
                    _windowHandle,
                    screenDc,
                    ref destination,
                    ref size,
                    memoryDc,
                    ref source,
                    0,
                    ref blend,
                    UlwAlpha))
            {
                throw new Win32Exception(
                    Marshal.GetLastPInvokeError(),
                    "Unable to update the anti-aliased overlay surface.");
            }

            var flags = NativeConstants.SwpNoActivate |
                        NativeConstants.SwpNoOwnerZOrder |
                        (visible ? NativeConstants.SwpShowWindow : 0);
            if (!NativeMethods.SetWindowPos(
                    _windowHandle,
                    mainWindow,
                    placement.X,
                    placement.Y,
                    placement.Width,
                    placement.Height,
                    flags))
            {
                throw new Win32Exception(
                    Marshal.GetLastPInvokeError(),
                    "Unable to position the anti-aliased overlay surface.");
            }

            if (!visible)
            {
                _ = NativeMethods.ShowWindow(_windowHandle, NativeConstants.SwHide);
            }
        }
        finally
        {
            if (previousBitmap != 0)
            {
                _ = SelectObject(memoryDc, previousBitmap);
            }

            if (bitmap != 0)
            {
                _ = NativeMethods.DeleteObject(bitmap);
            }

            _ = DeleteDC(memoryDc);
            _ = ReleaseDC(0, screenDc);
        }
    }

    internal void Hide()
    {
        if (!_disposed && _windowHandle != 0)
        {
            _ = NativeMethods.ShowWindow(_windowHandle, NativeConstants.SwHide);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_windowHandle != 0)
        {
            _ = DestroyWindow(_windowHandle);
            _windowHandle = 0;
        }

        _disposed = true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        internal int Width;
        internal int Height;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct NativeBlendFunction
    {
        internal byte BlendOperation;
        internal byte BlendFlags;
        internal byte SourceConstantAlpha;
        internal byte AlphaFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        internal uint Size;
        internal int Width;
        internal int Height;
        internal ushort Planes;
        internal ushort BitCount;
        internal uint Compression;
        internal uint SizeImage;
        internal int XPixelsPerMeter;
        internal int YPixelsPerMeter;
        internal uint ColorsUsed;
        internal uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        internal BitmapInfoHeader Header;
        internal uint Colors;

        internal static BitmapInfo Create(int width, int height) => new()
        {
            Header = new BitmapInfoHeader
            {
                Size = checked((uint)Marshal.SizeOf<BitmapInfoHeader>()),
                Width = width,
                Height = -height,
                Planes = 1,
                BitCount = 32,
                Compression = BiRgb,
                SizeImage = checked((uint)(width * height * 4))
            }
        };
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(nint windowHandle);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetDC(nint windowHandle);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(nint windowHandle, nint deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateCompatibleDC(nint deviceContext);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(nint deviceContext);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern nint CreateDIBSection(
        nint deviceContext,
        ref BitmapInfo bitmapInfo,
        uint usage,
        out nint bits,
        nint section,
        uint offset);

    [DllImport("gdi32.dll")]
    private static extern nint SelectObject(nint deviceContext, nint graphicsObject);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UpdateLayeredWindow(
        nint windowHandle,
        nint destinationDeviceContext,
        ref NativePoint destination,
        ref NativeSize size,
        nint sourceDeviceContext,
        ref NativePoint source,
        uint colorKey,
        ref NativeBlendFunction blend,
        uint flags);
}
