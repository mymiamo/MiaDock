using MiaDock.Core.Focus;
using MiaDock.Core.Modules;
using MiaDock.Core.Settings;

namespace MiaDock.Core.Tests;

[TestClass]
public sealed class FocusPolicyServiceTests
{
    [TestMethod]
    public void InactiveFocus_IsFullyPermissive()
    {
        var focus = new FakeFocusService(FocusSnapshot.Empty);
        using var policy = new FocusPolicyService(focus);
        var mediaEvent = Event("media", ModuleEventPriority.Low);

        Assert.IsFalse(policy.Current.IsActive);
        Assert.IsTrue(policy.Current.AllowsModule("media"));
        Assert.IsTrue(policy.Current.AllowsEvent(mediaEvent));
        Assert.IsTrue(policy.Current.AllowFullscreenNotifications);
        Assert.IsTrue(policy.Current.AllowSensitiveContentInFullscreen);
        Assert.IsTrue(policy.Current.AllowSensitiveContentWhenLocked);
    }

    [TestMethod]
    public void ActiveProfile_MapsEveryBehaviorConstraint()
    {
        var profile = FocusProfileDefaults.ForKind(FocusProfileKind.Sleep);
        var focus = new FakeFocusService(Active(profile));
        using var policy = new FocusPolicyService(focus);

        Assert.IsTrue(policy.Current.IsActive);
        Assert.AreEqual(profile.Id, policy.Current.ProfileId);
        Assert.AreEqual(FocusDockVisibility.EventsOnly, policy.Current.DockVisibility);
        Assert.IsTrue(policy.Current.AllowsModule("battery"));
        Assert.IsFalse(policy.Current.AllowsModule("media"));
        Assert.IsFalse(policy.Current.AllowsEvent(
            Event("battery", ModuleEventPriority.High)));
        Assert.IsTrue(policy.Current.AllowsEvent(
            Event("battery", ModuleEventPriority.Critical)));
        Assert.IsFalse(policy.Current.AllowFullscreenNotifications);
        Assert.IsFalse(policy.Current.AllowSensitiveContentInFullscreen);
        Assert.IsFalse(policy.Current.AllowSensitiveContentWhenLocked);
    }

    [TestMethod]
    public void FocusChange_PublishesOnlyWhenEffectivePolicyChanges()
    {
        var profile = FocusProfileDefaults.ForKind(FocusProfileKind.Work);
        var focus = new FakeFocusService(Active(profile));
        using var policy = new FocusPolicyService(focus);
        var changeCount = 0;
        policy.PolicyChanged += (_, _) => changeCount++;

        focus.Publish(Active(profile, DateTimeOffset.UtcNow.AddMinutes(30)));
        Assert.AreEqual(0, changeCount);

        focus.Publish(Active(FocusProfileDefaults.ForKind(FocusProfileKind.Gaming)));
        Assert.AreEqual(1, changeCount);
        Assert.AreEqual(FocusProfileDefaults.GamingId, policy.Current.ProfileId);

        focus.Publish(FocusSnapshot.Empty);
        Assert.AreEqual(2, changeCount);
        Assert.AreEqual(FocusPolicySnapshot.Inactive, policy.Current);
    }

    [TestMethod]
    public void Dispose_DetachesFromFocusChanges()
    {
        var focus = new FakeFocusService(FocusSnapshot.Empty);
        var policy = new FocusPolicyService(focus);
        var changeCount = 0;
        policy.PolicyChanged += (_, _) => changeCount++;

        policy.Dispose();
        focus.Publish(Active(FocusProfileDefaults.ForKind(FocusProfileKind.Work)));

        Assert.AreEqual(0, changeCount);
        Assert.IsFalse(policy.Current.IsActive);
    }

    [TestMethod]
    public void DockVisibility_CombinesFocusAndGlobalRules()
    {
        var always = PolicyWithVisibility(FocusDockVisibility.AlwaysVisible);
        var eventsOnly = PolicyWithVisibility(FocusDockVisibility.EventsOnly);
        var hidden = PolicyWithVisibility(FocusDockVisibility.Hidden);

        Assert.IsTrue(always.AllowsNormalDock(globalAlwaysVisible: false));
        Assert.IsFalse(eventsOnly.AllowsNormalDock(globalAlwaysVisible: true));
        Assert.IsTrue(eventsOnly.AllowsTemporaryDock(isFullscreen: false));
        Assert.IsFalse(hidden.AllowsNormalDock(globalAlwaysVisible: true));
        Assert.IsFalse(hidden.AllowsTemporaryDock(isFullscreen: false));
        Assert.IsTrue(FocusPolicySnapshot.Inactive.AllowsNormalDock(
            globalAlwaysVisible: true));
        Assert.IsFalse(FocusPolicySnapshot.Inactive.AllowsNormalDock(
            globalAlwaysVisible: false));
    }

    [TestMethod]
    public void DoNotDisturb_DefaultDoesNotHideAnAlwaysVisibleDock()
    {
        var focus = new FakeFocusService(Active(
            FocusProfileDefaults.ForKind(FocusProfileKind.DoNotDisturb)));
        using var policy = new FocusPolicyService(focus);

        Assert.AreEqual(
            FocusDockVisibility.UseGlobalSetting,
            policy.Current.DockVisibility);
        Assert.IsTrue(policy.Current.AllowsNormalDock(globalAlwaysVisible: true));
    }

    [TestMethod]
    public void FocusAccessPolicy_RequiresTrayEscapeOnlyWhenDockCanDisappear()
    {
        var profile = FocusProfileDefaults.ForKind(FocusProfileKind.Work);

        Assert.IsFalse(FocusAccessPolicy.RequiresTrayEscape(
            FocusSnapshot.Empty,
            IslandVisibilityMode.EventsOnly));
        Assert.IsFalse(FocusAccessPolicy.RequiresTrayEscape(
            Active(WithDockVisibility(profile, FocusDockVisibility.AlwaysVisible)),
            IslandVisibilityMode.EventsOnly));
        Assert.IsFalse(FocusAccessPolicy.RequiresTrayEscape(
            Active(WithDockVisibility(profile, FocusDockVisibility.UseGlobalSetting)),
            IslandVisibilityMode.Always));
        Assert.IsTrue(FocusAccessPolicy.RequiresTrayEscape(
            Active(WithDockVisibility(profile, FocusDockVisibility.UseGlobalSetting)),
            IslandVisibilityMode.EventsOnly));
        Assert.IsTrue(FocusAccessPolicy.RequiresTrayEscape(
            Active(WithDockVisibility(profile, FocusDockVisibility.EventsOnly)),
            IslandVisibilityMode.Always));
        Assert.IsTrue(FocusAccessPolicy.RequiresTrayEscape(
            Active(WithDockVisibility(profile, FocusDockVisibility.Hidden)),
            IslandVisibilityMode.Always));
    }

    [TestMethod]
    public void FullscreenNotificationPermission_IsRestrictive()
    {
        var profile = PolicyWithVisibility(
            FocusDockVisibility.EventsOnly,
            allowFullscreenNotifications: false);

        Assert.IsTrue(profile.AllowsTemporaryDock(isFullscreen: false));
        Assert.IsFalse(profile.AllowsTemporaryDock(isFullscreen: true));
    }

    private static FocusSnapshot Active(
        FocusProfile profile,
        DateTimeOffset? endsAtUtc = null) =>
        new(
            FocusProfileDefaults.All,
            profile,
            new FocusActivationState(
                profile.Id,
                FocusActivationSource.Manual,
                DateTimeOffset.Parse("2026-07-29T12:00:00Z"),
                endsAtUtc));

    private static ModuleEvent Event(
        string moduleId,
        ModuleEventPriority priority)
    {
        var occurredAt = DateTimeOffset.Parse("2026-07-29T12:00:00Z");
        return new ModuleEvent(
            moduleId,
            ModuleEventKind.PlaybackChanged,
            new ModulePresentation(
                moduleId,
                "title",
                string.Empty,
                string.Empty,
                ModuleIndicatorKind.None),
            TimeSpan.FromSeconds(5),
            occurredAt,
            priority,
            expiresAtUtc: occurredAt.AddMinutes(1));
    }

    private static FocusPolicySnapshot PolicyWithVisibility(
        FocusDockVisibility visibility,
        bool allowFullscreenNotifications = true) =>
        new(
            true,
            "test",
            visibility,
            new HashSet<string>(StringComparer.Ordinal),
            ModuleEventPriority.Low,
            allowFullscreenNotifications,
            true,
            true);

    private static FocusProfile WithDockVisibility(
        FocusProfile profile,
        FocusDockVisibility visibility) =>
        profile with
        {
            Behavior = profile.Behavior with
            {
                DockVisibility = visibility
            }
        };

    private sealed class FakeFocusService(FocusSnapshot current) : IFocusService
    {
        public FocusSnapshot Current { get; private set; } = current;

        public event EventHandler<FocusChangedEventArgs>? FocusChanged;

        public void Publish(FocusSnapshot current)
        {
            var previous = Current;
            Current = current;
            FocusChanged?.Invoke(
                this,
                new FocusChangedEventArgs(
                    previous,
                    current,
                    FocusChangeReason.Switched));
        }

        public void Start()
        {
        }

        public bool Activate(
            string profileId,
            FocusActivationSource source = FocusActivationSource.Manual) =>
            false;

        public bool ActivateFor(
            string profileId,
            TimeSpan duration,
            FocusActivationSource source = FocusActivationSource.Manual) =>
            false;

        public bool ActivateIndefinitely(
            string profileId,
            FocusActivationSource source = FocusActivationSource.Manual) =>
            false;

        public bool Deactivate() => false;

        public bool Refresh() => false;

        public void Dispose()
        {
        }
    }
}
