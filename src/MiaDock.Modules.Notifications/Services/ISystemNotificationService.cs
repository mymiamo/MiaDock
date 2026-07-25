using MiaDock.Modules.Notifications.Models;

namespace MiaDock.Modules.Notifications.Services;

public interface ISystemNotificationService : IAsyncDisposable
{
    NotificationAccessState AccessState { get; }
    IReadOnlyList<NotificationSourceInfo> Sources { get; }

    event EventHandler<NotificationAccessState>? AccessStateChanged;
    event EventHandler<IReadOnlyList<NotificationSourceInfo>>? SourcesChanged;
    event EventHandler<SystemNotificationSnapshot>? NotificationReceived;

    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<NotificationAccessState> RequestAccessAsync(CancellationToken cancellationToken = default);
}
