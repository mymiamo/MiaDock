using MiaDock.Modules.Media.Models;

namespace MiaDock.Platform.Windows.Media;

public static class MediaSessionSelector
{
    public static MediaSessionDescriptor? Select(
        IEnumerable<MediaSessionDescriptor> sessions,
        MediaSelectionOptions selection)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(selection);

        var candidates = sessions.ToArray();
        if (selection.SelectedSourceId is not null)
        {
            var selectedSourceSessions = candidates
                .Where(item => string.Equals(
                    item.SourceId,
                    selection.SelectedSourceId,
                    StringComparison.Ordinal))
                .ToArray();

            if (selectedSourceSessions.Length > 0)
            {
                return Rank(selectedSourceSessions).First();
            }

            if (selection.FallbackBehavior == MediaFallbackBehavior.SelectedSourceOnly)
            {
                return null;
            }
        }

        return Rank(candidates).FirstOrDefault();
    }

    private static IOrderedEnumerable<MediaSessionDescriptor> Rank(
        IEnumerable<MediaSessionDescriptor> sessions) =>
        sessions
            .OrderByDescending(item => item.PlaybackStatus == PlaybackStatus.Playing)
            .ThenByDescending(item => item.IsSystemCurrent)
            .ThenByDescending(item => item.LastUpdatedAt)
            .ThenBy(item => item.SessionKey, StringComparer.Ordinal);
}
