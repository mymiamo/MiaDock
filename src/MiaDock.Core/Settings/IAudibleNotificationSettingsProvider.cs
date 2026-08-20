namespace MiaDock.Core.Settings;

public interface IAudibleNotificationSettingsProvider
{
    AudibleNotificationSettings Current { get; }
}
