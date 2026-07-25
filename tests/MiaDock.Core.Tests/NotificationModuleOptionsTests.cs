using MiaDock.Modules.Notifications.Settings;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class NotificationModuleOptionsTests
{
    [TestMethod]
    public void Default_HidesBodyAndAllowsSourcesWithoutAnAllowList()
    {
        var options = NotificationModuleOptions.Default;

        Assert.IsFalse(options.IsEnabled);
        Assert.IsFalse(options.CanShowBody("mail"));
        Assert.IsTrue(options.IsApplicationAllowed("mail"));
    }

    [TestMethod]
    public void Filtering_RespectsAllowAndBlockLists()
    {
        var options = NotificationModuleOptions.Default with
        {
            UseAllowList = true,
            AllowedApplications = new HashSet<string>(StringComparer.Ordinal) { "mail", "chat" },
            BlockedApplications = new HashSet<string>(StringComparer.Ordinal) { "chat" },
            BodyAllowedApplications = new HashSet<string>(StringComparer.Ordinal) { "mail" }
        };

        Assert.IsTrue(options.IsApplicationAllowed("mail"));
        Assert.IsFalse(options.IsApplicationAllowed("chat"));
        Assert.IsFalse(options.IsApplicationAllowed("calendar"));
        Assert.IsTrue(options.CanShowBody("mail"));
        Assert.IsFalse(options.CanShowBody("chat"));
    }

    [TestMethod]
    public void EnvelopeRoundTrip_PreservesPrivacyOptions()
    {
        var expected = NotificationModuleOptions.Default with
        {
            IsEnabled = true,
            EventDuration = TimeSpan.FromSeconds(9),
            ShowInFullscreen = true,
            UseAllowList = true,
            AllowedApplications = new HashSet<string>(StringComparer.Ordinal) { "mail" },
            BodyAllowedApplications = new HashSet<string>(StringComparer.Ordinal) { "mail" }
        };

        var actual = NotificationModuleOptions.FromEnvelope(NotificationModuleOptions.ToEnvelope(expected));

        Assert.IsTrue(actual.IsEnabled);
        Assert.AreEqual(TimeSpan.FromSeconds(9), actual.EventDuration);
        Assert.IsTrue(actual.ShowInFullscreen);
        Assert.IsTrue(actual.UseAllowList);
        Assert.IsTrue(actual.AllowedApplications.SetEquals(["mail"]));
        Assert.IsTrue(actual.BodyAllowedApplications.SetEquals(["mail"]));
    }
}
