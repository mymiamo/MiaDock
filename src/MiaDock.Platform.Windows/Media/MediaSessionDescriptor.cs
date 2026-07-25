using MiaDock.Modules.Media.Models;

namespace MiaDock.Platform.Windows.Media;

public sealed record MediaSessionDescriptor(
    string SessionKey,
    string SourceId,
    PlaybackStatus PlaybackStatus,
    bool IsSystemCurrent,
    DateTimeOffset LastUpdatedAt);
