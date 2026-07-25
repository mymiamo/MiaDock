using MiaDock.Core.Settings;

namespace MiaDock.Platform.Windows.Display;

public interface IDisplayTopologyService : IDisposable
{
    IReadOnlyList<DisplayDescriptor> Displays { get; }

    DisplayDescriptor Primary { get; }

    event EventHandler<IReadOnlyList<DisplayDescriptor>>? DisplaysChanged;

    void Start();

    DisplayDescriptor Resolve(MonitorSettings settings, nint foregroundWindow = 0);

    DisplayDescriptor ResolveForWindow(nint windowHandle);

    DisplayDescriptor? Find(string? displayId);
}
