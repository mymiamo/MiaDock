using MiaDock.Core.Settings;

namespace MiaDock.App.Services;

public sealed class AudibleNotificationSettingsProvider(ISettingsService settings) : IAudibleNotificationSettingsProvider
{
    public AudibleNotificationSettings Current => settings.Current.AudibleNotifications;
}
