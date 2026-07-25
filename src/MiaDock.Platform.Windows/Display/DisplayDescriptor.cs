using Windows.Graphics;

namespace MiaDock.Platform.Windows.Display;

public sealed record DisplayDescriptor(
    string Id,
    string DisplayName,
    RectInt32 Bounds,
    RectInt32 WorkArea,
    bool IsPrimary);
