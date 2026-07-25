namespace MiaDock.Modules.Notifications.Models;

public sealed record SystemNotificationSnapshot(
    uint Id,
    string SourceId,
    string SourceDisplayName,
    string Title,
    string Body,
    DateTimeOffset CreatedAtUtc);
