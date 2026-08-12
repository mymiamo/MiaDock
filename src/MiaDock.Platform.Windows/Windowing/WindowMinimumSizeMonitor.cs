using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using MiaDock.Platform.Windows.Interop;

namespace MiaDock.Platform.Windows.Windowing;

public sealed class WindowMinimumSizeMonitor : IDisposable
{
    private const uint WmGetMinMaxInfo = 0x0024;
    private const nuint SubclassId = 0x4D494D53;
    private const uint DefaultDpi = 96;

    private readonly Window _window;
    private readonly double _minimumWidthInDips;
    private readonly double _minimumHeightInDips;
    private readonly NativeMethods.SubclassProcedure _subclassProcedure;
    private readonly nint _windowHandle;
    private bool _subclassInstalled;
    private bool _disposed;

    public WindowMinimumSizeMonitor(
        Window window,
        double minimumWidthInDips,
        double minimumHeightInDips)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (!double.IsFinite(minimumWidthInDips) || minimumWidthInDips <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumWidthInDips));
        }

        if (!double.IsFinite(minimumHeightInDips) || minimumHeightInDips <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumHeightInDips));
        }

        _window = window;
        _minimumWidthInDips = minimumWidthInDips;
        _minimumHeightInDips = minimumHeightInDips;
        _subclassProcedure = WindowMessageHandler;
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (_windowHandle == 0)
        {
            throw new InvalidOperationException("Unable to retrieve the window handle for size constraints.");
        }

        if (!NativeMethods.SetWindowSubclass(_windowHandle, _subclassProcedure, SubclassId, 0))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Unable to apply the minimum window size.");
        }

        _subclassInstalled = true;
        _window.Closed += OnWindowClosed;
        EnsureCurrentSize();
    }

    internal static int ScaleDipToPixels(double valueInDips, uint dpi) =>
        checked((int)Math.Ceiling(valueInDips * (dpi == 0 ? DefaultDpi : dpi) / DefaultDpi));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _window.Closed -= OnWindowClosed;
        if (_subclassInstalled)
        {
            _ = NativeMethods.RemoveWindowSubclass(_windowHandle, _subclassProcedure, SubclassId);
            _subclassInstalled = false;
        }
    }

    private void EnsureCurrentSize()
    {
        var dpi = NativeMethods.GetDpiForWindow(_windowHandle);
        var minimumWidth = ScaleDipToPixels(_minimumWidthInDips, dpi);
        var minimumHeight = ScaleDipToPixels(_minimumHeightInDips, dpi);
        var size = _window.AppWindow.Size;
        if (size.Width < minimumWidth || size.Height < minimumHeight)
        {
            _window.AppWindow.Resize(new global::Windows.Graphics.SizeInt32(
                Math.Max(size.Width, minimumWidth),
                Math.Max(size.Height, minimumHeight)));
        }
    }

    private nint WindowMessageHandler(
        nint windowHandle,
        uint message,
        nuint wParam,
        nint lParam,
        nuint subclassId,
        nuint referenceData)
    {
        try
        {
            if (message == WmGetMinMaxInfo && lParam != 0)
            {
                var dpi = NativeMethods.GetDpiForWindow(windowHandle);
                var minMaxInfo = Marshal.PtrToStructure<NativeMinMaxInfo>(lParam);
                minMaxInfo.MinimumTrackSize.X = Math.Max(
                    minMaxInfo.MinimumTrackSize.X,
                    ScaleDipToPixels(_minimumWidthInDips, dpi));
                minMaxInfo.MinimumTrackSize.Y = Math.Max(
                    minMaxInfo.MinimumTrackSize.Y,
                    ScaleDipToPixels(_minimumHeightInDips, dpi));
                Marshal.StructureToPtr(minMaxInfo, lParam, false);
            }
        }
        catch (Exception)
        {
            // A managed exception must never cross the reverse P/Invoke WndProc
            // boundary; escaping here terminates the process through a native
            // fail-fast path that no handler can observe.
        }

        return NativeMethods.DefSubclassProc(windowHandle, message, wParam, lParam);
    }

    private void OnWindowClosed(object sender, WindowEventArgs args) => Dispose();

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaximumSize;
        public NativePoint MaximumPosition;
        public NativePoint MinimumTrackSize;
        public NativePoint MaximumTrackSize;
    }
}
