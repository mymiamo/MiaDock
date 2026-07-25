using MiaDock.Core.Modules;
using MiaDock.Modules.Notifications;
using MiaDock.Modules.Notifications.Models;
using MiaDock.Modules.Notifications.Services;
using MiaDock.Modules.Notifications.Settings;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class NotificationModuleTests
{
    [TestMethod]
    public async Task Notification_DefaultPresentationOmitsBodyAndUsesConfiguredDuration()
    {
        var service = new FakeNotificationService();
        var settings = new FakeNotificationSettings(NotificationModuleOptions.Default with
        {
            IsEnabled = true,
            EventDuration = TimeSpan.FromSeconds(8)
        });
        using var module = new NotificationModule(service, settings) { IsEnabled = true };
        ModuleEvent? received = null;
        module.EventOccurred += (_, value) => received = value;
        await module.ActivateAsync();

        service.Publish(CreateSnapshot());

        Assert.IsNotNull(received);
        Assert.AreEqual("Posta", received.Presentation.PrimaryText);
        Assert.AreEqual("Yeni ileti", received.Presentation.SecondaryText);
        Assert.IsNull(received.Presentation.ValueText);
        Assert.IsTrue(received.Presentation.IsSensitive);
        Assert.AreEqual(TimeSpan.FromSeconds(8), received.DisplayDuration);
        Assert.IsFalse(received.IsFullscreenEligible);
    }

    [TestMethod]
    public async Task Notification_BodyAndFullscreenRequireExplicitSettings()
    {
        var service = new FakeNotificationService();
        var settings = new FakeNotificationSettings(NotificationModuleOptions.Default with
        {
            IsEnabled = true,
            ShowInFullscreen = true,
            BodyAllowedApplications = new HashSet<string>(StringComparer.Ordinal) { "mail" }
        });
        using var module = new NotificationModule(service, settings) { IsEnabled = true };
        ModuleEvent? received = null;
        module.EventOccurred += (_, value) => received = value;
        await module.ActivateAsync();

        service.Publish(CreateSnapshot());

        Assert.IsNotNull(received);
        Assert.AreEqual("İleti gövdesi", received.Presentation.ValueText);
        Assert.IsTrue(received.IsFullscreenEligible);
    }

    [TestMethod]
    public async Task Notification_BlockedSourceDoesNotEmitAnEvent()
    {
        var service = new FakeNotificationService();
        var settings = new FakeNotificationSettings(NotificationModuleOptions.Default with
        {
            IsEnabled = true,
            BlockedApplications = new HashSet<string>(StringComparer.Ordinal) { "mail" }
        });
        using var module = new NotificationModule(service, settings) { IsEnabled = true };
        var eventCount = 0;
        module.EventOccurred += (_, _) => eventCount++;
        await module.ActivateAsync();

        service.Publish(CreateSnapshot());

        Assert.AreEqual(0, eventCount);
    }

    private static SystemNotificationSnapshot CreateSnapshot() => new(
        42,
        "mail",
        "Posta",
        "Yeni ileti",
        "İleti gövdesi",
        DateTimeOffset.UtcNow);

    private sealed class FakeNotificationSettings(NotificationModuleOptions options) : INotificationModuleSettings
    {
        public NotificationModuleOptions Current { get; } = options;
        public event EventHandler<NotificationModuleOptions>? Changed { add { } remove { } }
    }

    private sealed class FakeNotificationService : ISystemNotificationService
    {
        public NotificationAccessState AccessState => NotificationAccessState.Allowed;
        public IReadOnlyList<NotificationSourceInfo> Sources => [];
        public event EventHandler<NotificationAccessState>? AccessStateChanged { add { } remove { } }
        public event EventHandler<IReadOnlyList<NotificationSourceInfo>>? SourcesChanged { add { } remove { } }
        public event EventHandler<SystemNotificationSnapshot>? NotificationReceived;
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<NotificationAccessState> RequestAccessAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(NotificationAccessState.Allowed);
        public void Publish(SystemNotificationSnapshot snapshot) => NotificationReceived?.Invoke(this, snapshot);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
