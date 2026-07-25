using MiaDock.Modules.Notifications.Settings;

namespace MiaDock.App.Services;

public sealed class NotificationModuleSettingsAdapter : INotificationModuleSettings, IDisposable
{
    private readonly ISettingsService _settings;

    public NotificationModuleSettingsAdapter(ISettingsService settings)
    {
        _settings = settings;
        Current = ReadCurrent();
        _settings.SettingsChanged += OnSettingsChanged;
    }

    public NotificationModuleOptions Current { get; private set; }
    public event EventHandler<NotificationModuleOptions>? Changed;

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        var next = ReadCurrent();
        if (next == Current) return;
        Current = next;
        Changed?.Invoke(this, next);
    }

    private NotificationModuleOptions ReadCurrent() => NotificationModuleOptions.FromEnvelope(
        _settings.Current.Modules.TryGetValue("notifications", out var envelope) ? envelope : null);

    public void Dispose() => _settings.SettingsChanged -= OnSettingsChanged;
}
