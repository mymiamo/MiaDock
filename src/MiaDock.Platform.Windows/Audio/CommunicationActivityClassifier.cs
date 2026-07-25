using MiaDock.Modules.SystemStatus.Models;

namespace MiaDock.Platform.Windows.Audio;

public static class CommunicationActivityClassifier
{
    private static readonly string[] CommunicationProcessTokens =
    [
        "teams",
        "msteams",
        "zoom",
        "discord",
        "slack",
        "skype",
        "webex",
        "whatsapp",
        "signal"
    ];

    public static CallActivityState Classify(
        bool isMicrophoneActive,
        IEnumerable<string> activeAudioProcessNames)
    {
        ArgumentNullException.ThrowIfNull(activeAudioProcessNames);
        if (!isMicrophoneActive)
        {
            return CallActivityState.None;
        }

        return activeAudioProcessNames
            .Select(MediaAudioSessionMatcher.Normalize)
            .Any(name => CommunicationProcessTokens.Any(token => name.Contains(token, StringComparison.Ordinal)))
            ? CallActivityState.Possible
            : CallActivityState.None;
    }
}
