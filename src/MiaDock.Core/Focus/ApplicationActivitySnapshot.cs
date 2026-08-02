namespace MiaDock.Core.Focus;

public sealed record FocusApplicationInfo(
    string Target,
    string DisplayName);

public sealed record ApplicationActivitySnapshot(
    string? ForegroundTarget,
    IReadOnlySet<string> RunningTargets,
    IReadOnlyList<FocusApplicationInfo> AvailableApplications,
    bool IsProcessMonitoringAvailable)
{
    public static ApplicationActivitySnapshot Empty { get; } = new(
        null,
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        Array.Empty<FocusApplicationInfo>(),
        false);

    public bool IsRunning(string? target) =>
        !string.IsNullOrWhiteSpace(target) &&
        RunningTargets.Contains(FocusApplicationTarget.Normalize(target));

    public bool IsForeground(string? target) =>
        !string.IsNullOrWhiteSpace(target) &&
        string.Equals(
            ForegroundTarget,
            FocusApplicationTarget.Normalize(target),
            StringComparison.OrdinalIgnoreCase);
}
