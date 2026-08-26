using MiaDock.Modules.SystemStatus.Models;

namespace MiaDock.Platform.Windows.Audio;

public sealed record CommunicationProcessIdentity(
    string ProcessName,
    string? ExecutablePath = null);

public sealed record CommunicationActivityResult(
    CallActivityState State,
    CommunicationApp App,
    string? DisplayName,
    string? ProcessName,
    string? ExecutablePath,
    bool HasCaptureActivity,
    bool HasRenderActivity)
{
    public static CommunicationActivityResult None { get; } = new(
        CallActivityState.None,
        CommunicationApp.Unknown,
        null,
        null,
        null,
        false,
        false);

    public CallActivityInfo ToCallInfo(DateTimeOffset? startedAt = null) => new(
        State,
        App,
        DisplayName,
        ProcessName,
        ExecutablePath,
        startedAt,
        HasCaptureActivity,
        HasRenderActivity);
}

public static class CommunicationActivityClassifier
{
    private sealed record Descriptor(
        CommunicationApp App,
        string DisplayName,
        bool SupportsStrongDetection,
        params string[] ProcessAliases);

    private static readonly Descriptor[] Descriptors =
    [
        new(CommunicationApp.Discord, "Discord", true,
            "discord", "discordptb", "discordcanary", "discorddevelopment", "discordsystemhelper"),
        new(CommunicationApp.Skype, "Skype", true,
            "skype", "skypeapp", "skypehost", "microsoftskypeapp"),
        new(CommunicationApp.WhatsApp, "WhatsApp", true,
            "whatsapp", "whatsappdesktop", "whatsappnative", "whatsappbeta", "whatsapproot"),
        new(CommunicationApp.Teams, "Microsoft Teams", false,
            "teams", "msteams", "microsoftteams"),
        new(CommunicationApp.Zoom, "Zoom", false,
            "zoom", "zoommeeting"),
        new(CommunicationApp.Webex, "Webex", false,
            "webex", "webexhost", "ciscocollabhost"),
        new(CommunicationApp.Slack, "Slack", false,
            "slack"),
        new(CommunicationApp.Signal, "Signal", false,
            "signal")
    ];

    public static CommunicationActivityResult Classify(
        IEnumerable<CommunicationProcessIdentity> activeCaptureProcesses,
        IEnumerable<CommunicationProcessIdentity> activeRenderProcesses,
        CommunicationApp preferredApp = CommunicationApp.Unknown)
    {
        ArgumentNullException.ThrowIfNull(activeCaptureProcesses);
        ArgumentNullException.ThrowIfNull(activeRenderProcesses);

        var captures = Resolve(activeCaptureProcesses);
        var renders = Resolve(activeRenderProcesses);
        var strongDescriptors = Descriptors.Where(descriptor =>
                descriptor.SupportsStrongDetection &&
                captures.Any(item => item.Descriptor.App == descriptor.App) &&
                renders.Any(item => item.Descriptor.App == descriptor.App))
            .ToArray();
        var strong = preferredApp == CommunicationApp.Unknown
            ? strongDescriptors.FirstOrDefault()
            : strongDescriptors.FirstOrDefault(descriptor => descriptor.App == preferredApp) ??
              strongDescriptors.FirstOrDefault();

        if (strong is not null)
        {
            var capture = captures.First(item => item.Descriptor.App == strong.App).Identity;
            var render = renders.First(item => item.Descriptor.App == strong.App).Identity;
            return new CommunicationActivityResult(
                CallActivityState.Detected,
                strong.App,
                strong.DisplayName,
                capture.ProcessName,
                FirstNonEmpty(capture.ExecutablePath, render.ExecutablePath),
                true,
                true);
        }

        var possible = captures.FirstOrDefault();
        if (possible is null)
        {
            return CommunicationActivityResult.None;
        }

        var hasMatchingRender = renders.Any(item => item.Descriptor.App == possible.Descriptor.App);
        return new CommunicationActivityResult(
            CallActivityState.Possible,
            possible.Descriptor.App,
            possible.Descriptor.DisplayName,
            possible.Identity.ProcessName,
            possible.Identity.ExecutablePath,
            true,
            hasMatchingRender);
    }

    private static List<ResolvedIdentity> Resolve(IEnumerable<CommunicationProcessIdentity> identities)
    {
        var result = new List<ResolvedIdentity>();
        foreach (var identity in identities)
        {
            if (string.IsNullOrWhiteSpace(identity.ProcessName)) continue;
            var normalized = MediaAudioSessionMatcher.Normalize(identity.ProcessName);
            var descriptor = Descriptors.FirstOrDefault(candidate =>
                candidate.ProcessAliases.Contains(normalized, StringComparer.Ordinal));
            if (descriptor is not null)
            {
                result.Add(new ResolvedIdentity(descriptor, identity));
            }
        }
        return result;
    }

    private static string? FirstNonEmpty(string? first, string? second) =>
        !string.IsNullOrWhiteSpace(first) ? first :
        !string.IsNullOrWhiteSpace(second) ? second : null;

    private sealed record ResolvedIdentity(
        Descriptor Descriptor,
        CommunicationProcessIdentity Identity);
}
