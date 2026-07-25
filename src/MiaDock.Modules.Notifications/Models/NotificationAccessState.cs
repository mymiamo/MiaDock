namespace MiaDock.Modules.Notifications.Models;

public enum NotificationAccessState
{
    Uninitialized,
    Unsupported,
    PackageIdentityRequired,
    Unspecified,
    Allowed,
    Denied,
    Faulted
}
