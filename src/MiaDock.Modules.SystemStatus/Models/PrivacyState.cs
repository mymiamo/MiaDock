namespace MiaDock.Modules.SystemStatus.Models;

public sealed record PrivacyState(
    bool MicrophoneInUse,
    bool CameraInUse,
    IReadOnlyList<PrivacyApplication> ActiveApplications,
    PrivacyIndicatorKind Indicator)
{
    public static PrivacyState Empty { get; } = new(
        false,
        false,
        Array.Empty<PrivacyApplication>(),
        PrivacyIndicatorKind.Idle);

    public IReadOnlyList<PrivacyApplication> ActiveMicrophoneApps =>
        ActiveApplications.Where(app => app.UsesMicrophone).ToArray();

    public IReadOnlyList<PrivacyApplication> ActiveCameraApps =>
        ActiveApplications.Where(app => app.UsesCamera).ToArray();

    public static PrivacyState FromApplications(IEnumerable<PrivacyApplication> applications)
    {
        ArgumentNullException.ThrowIfNull(applications);

        var apps = applications
            .Where(app => app.UsesMicrophone || app.UsesCamera)
            .GroupBy(app => app.Id, StringComparer.OrdinalIgnoreCase)
            .Select(MergeGroup)
            .OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();

        var microphoneInUse = apps.Any(app => app.UsesMicrophone);
        var cameraInUse = apps.Any(app => app.UsesCamera);
        return new(
            microphoneInUse,
            cameraInUse,
            apps,
            ResolveIndicator(cameraInUse, microphoneInUse));
    }

    public static PrivacyIndicatorKind ResolveIndicator(bool cameraInUse, bool microphoneInUse)
    {
        if (cameraInUse)
        {
            return PrivacyIndicatorKind.Camera;
        }

        if (microphoneInUse)
        {
            return PrivacyIndicatorKind.Microphone;
        }

        return PrivacyIndicatorKind.Idle;
    }

    private static PrivacyApplication MergeGroup(IGrouping<string, PrivacyApplication> group)
    {
        var first = group.First();
        return first with
        {
            ProcessId = group.Select(item => item.ProcessId).FirstOrDefault(id => id is > 0),
            ProcessName = group.Select(item => item.ProcessName)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? first.ProcessName,
            DisplayName = group.Select(item => item.DisplayName)
                .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? first.DisplayName,
            ExecutablePath = group.Select(item => item.ExecutablePath)
                .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path)),
            UsesMicrophone = group.Any(item => item.UsesMicrophone),
            UsesCamera = group.Any(item => item.UsesCamera)
        };
    }
}
