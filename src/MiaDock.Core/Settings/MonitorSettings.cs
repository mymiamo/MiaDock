namespace MiaDock.Core.Settings;

public sealed record MonitorSettings(MonitorSelectionMode Mode, string? FixedMonitorId)
{
    public static MonitorSettings Default { get; } = new(MonitorSelectionMode.Primary, null);
}
