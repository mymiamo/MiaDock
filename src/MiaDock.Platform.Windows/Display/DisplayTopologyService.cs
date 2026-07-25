using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using MiaDock.Core.Settings;
using MiaDock.Platform.Windows.Interop;

namespace MiaDock.Platform.Windows.Display;

public sealed class DisplayTopologyService : IDisplayTopologyService
{
    private DisplayAreaWatcher? _watcher;
    private bool _disposed;

    public IReadOnlyList<DisplayDescriptor> Displays { get; private set; } = Array.Empty<DisplayDescriptor>();

    public DisplayDescriptor Primary => Displays.FirstOrDefault(display => display.IsPrimary)
        ?? Displays.FirstOrDefault()
        ?? throw new InvalidOperationException("Windows did not report an available display.");

    public event EventHandler<IReadOnlyList<DisplayDescriptor>>? DisplaysChanged;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_watcher is not null) return;

        Refresh();
        _watcher = DisplayArea.CreateWatcher();
        _watcher.Added += OnDisplayChanged;
        _watcher.Removed += OnDisplayRemoved;
        _watcher.Updated += OnDisplayChanged;
        _watcher.EnumerationCompleted += OnEnumerationCompleted;
        _watcher.Start();
    }

    public DisplayDescriptor Resolve(MonitorSettings settings, nint foregroundWindow = 0)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Mode switch
        {
            MonitorSelectionMode.Fixed => Find(settings.FixedMonitorId) ?? Primary,
            MonitorSelectionMode.ActiveWindow when foregroundWindow != 0 => ResolveForWindow(foregroundWindow),
            _ => Primary
        };
    }

    public DisplayDescriptor ResolveForWindow(nint windowHandle)
    {
        if (windowHandle == 0) return Primary;
        var monitor = NativeMethods.MonitorFromWindow(windowHandle, NativeConstants.MonitorDefaultToNearest);
        if (monitor == 0) return Primary;
        var info = NativeMonitorInfo.Create();
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Unable to resolve the window display.");
        }

        return Displays.FirstOrDefault(display => BoundsEqual(display.Bounds, info.Monitor)) ?? Primary;
    }

    public DisplayDescriptor? Find(string? displayId) => string.IsNullOrWhiteSpace(displayId)
        ? null
        : Displays.FirstOrDefault(display => string.Equals(display.Id, displayId, StringComparison.Ordinal));

    public void Dispose()
    {
        if (_disposed) return;
        if (_watcher is not null)
        {
            _watcher.Added -= OnDisplayChanged;
            _watcher.Removed -= OnDisplayRemoved;
            _watcher.Updated -= OnDisplayChanged;
            _watcher.EnumerationCompleted -= OnEnumerationCompleted;
            if (_watcher.Status is DisplayAreaWatcherStatus.Started or DisplayAreaWatcherStatus.EnumerationCompleted)
            {
                _watcher.Stop();
            }

            _watcher = null;
        }

        _disposed = true;
    }

    private void Refresh()
    {
        var areas = DisplayArea.FindAll();
        var displays = new List<DisplayDescriptor>(areas.Count);
        for (var index = 0; index < areas.Count; index++)
        {
            var area = areas[index];
            displays.Add(new DisplayDescriptor(
                area.DisplayId.Value.ToString("X16"),
                area.IsPrimary ? $"Ekran {index + 1} (Ana)" : $"Ekran {index + 1}",
                area.OuterBounds,
                area.WorkArea,
                area.IsPrimary));
        }

        Displays = displays
            .OrderByDescending(display => display.IsPrimary)
            .ThenBy(display => display.Bounds.X)
            .ThenBy(display => display.Bounds.Y)
            .ToArray();
        DisplaysChanged?.Invoke(this, Displays);
    }

    private void OnDisplayChanged(DisplayAreaWatcher sender, DisplayArea args) => Refresh();

    private void OnDisplayRemoved(DisplayAreaWatcher sender, DisplayArea args) => Refresh();

    private void OnEnumerationCompleted(DisplayAreaWatcher sender, object args) => Refresh();

    private static bool BoundsEqual(global::Windows.Graphics.RectInt32 left, NativeRect right) =>
        left.X == right.Left && left.Y == right.Top && left.Width == right.Width && left.Height == right.Height;
}
