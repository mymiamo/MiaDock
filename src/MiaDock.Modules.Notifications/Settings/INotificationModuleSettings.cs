namespace MiaDock.Modules.Notifications.Settings;

public interface INotificationModuleSettings
{
    NotificationModuleOptions Current { get; }
    event EventHandler<NotificationModuleOptions>? Changed;
}
